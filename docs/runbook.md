# Runbook — RentifyX Identity API

**Version:** v1.0.0
**On-call channel:** `#rentifyx-oncall` (placeholder)
**Escalation contact:** Platform Lead (placeholder)

---

## 1. Deployment

### Prerequisites

- `kubectl` configured for the target EKS cluster
- `terraform output` values available (IAM role ARN, table name)
- Secrets populated in AWS Secrets Manager (`rentifyx-identity-production/*`)
- Fluent Bit DaemonSet running (CloudWatch Logs log group `/rentifyx/identity-api` must exist)

### Steps

```bash
# 1. Build and push the container image
docker build -t <ECR_REPO>:v1.0.0 .
docker push <ECR_REPO>:v1.0.0

# 2. Update the image tag in the prod overlay
# k8s/overlays/prod/patch-deployment.yaml → image: <ECR_REPO>:v1.0.0

# 3. Replace the IAM role ARN placeholder
# k8s/overlays/prod/patch-deployment.yaml
# eks.amazonaws.com/role-arn: <value from `terraform output iam_role_arn`>

# 4. Apply the prod overlay
kubectl kustomize k8s/overlays/prod | kubectl apply -f -

# 5. Verify the rollout
kubectl rollout status deployment/api -n prod --timeout=5m
```

**Expected output:** `deployment "api" successfully rolled out`

### Verify health

```bash
kubectl get pods -n prod -l app=api
kubectl logs -n prod -l app=api --tail=50
curl -s https://<LOAD_BALANCER_DNS>/health/ready   # expect: {"status":"Healthy"}
curl -s https://<LOAD_BALANCER_DNS>/health/live    # expect: {"status":"Healthy"}
```

---

## 2. Rollback

Use when a deployment causes elevated error rate or failed health probes.

```bash
# Roll back to the previous ReplicaSet
kubectl rollout undo deployment/api -n prod

# Confirm rollback is complete
kubectl rollout status deployment/api -n prod --timeout=5m

# Verify health probes pass after rollback
curl -s https://<LOAD_BALANCER_DNS>/health/ready
```

To roll back to a specific revision:

```bash
kubectl rollout history deployment/api -n prod
kubectl rollout undo deployment/api -n prod --to-revision=<N>
```

---

## 3. Secrets Rotation

### JWT Private Key PEM

```bash
# 1. Generate a new RSA-2048 key pair
openssl genrsa -out new-private-key.pem 2048
openssl rsa -in new-private-key.pem -pubout -out new-public-key.pem

# 2. Update the secret in Secrets Manager (Terraform lifecycle.ignore_changes keeps it safe)
aws secretsmanager put-secret-value \
  --secret-id "rentifyx-identity-production/jwt-private-key-pem" \
  --secret-string file://new-private-key.pem

# 3. Restart pods to reload the secret (CSI driver remounts on pod restart)
kubectl rollout restart deployment/api -n prod
kubectl rollout status deployment/api -n prod --timeout=5m

# 4. Verify: existing JWTs signed with the old key will be rejected immediately.
#    Ensure downstream services have a grace period or re-authenticate before rotating.
```

### HMAC Key

Same procedure as JWT key. Note: rotating the HMAC key invalidates all in-flight email verification tokens and password reset tokens. Schedule during low-traffic window.

### SES From Address

```bash
aws secretsmanager put-secret-value \
  --secret-id "rentifyx-identity-production/ses-from-address" \
  --secret-string "new-sender@example.com"

kubectl rollout restart deployment/api -n prod
```

---

## 4. Health Check Reference

| Endpoint | Expected response | Notes |
|---|---|---|
| `GET /health/ready` | `200 {"status":"Healthy"}` | Fails if DynamoDB or Secrets Manager unreachable |
| `GET /health/live` | `200 {"status":"Healthy"}` | Fails only on process-level issues |
| `GET /api/v1/auth/register` (POST) | `201` or `422` | Smoke test: send a valid registration payload |

---

## 5. Known Failure Modes

### DynamoDB throttling

**Symptom:** `ProvisionedThroughputExceededException` in logs; 5xx spike.
**Cause:** Unexpected. Table uses `PAY_PER_REQUEST` — throttling should not occur under normal load.
**Action:** Check CloudWatch DynamoDB metrics for `ConsumedWriteCapacityUnits`. If a GSI is causing it, run `aws dynamodb describe-table` to inspect. Contact AWS support if sustained.

### SES send rate limit

**Symptom:** `MessageRejected` or `ThrottlingException` from SES in logs.
**Cause:** Registration burst exceeding SES sending quota.
**Action:** Check SES sending quota in the console (`SES > Account dashboard`). Request quota increase via AWS support. Temporarily add exponential back-off in `EmailService` if needed.

### Secrets Manager cold-start latency

**Symptom:** Pod takes > 30 s to become ready on first start; readiness probe fails.
**Cause:** `SecretsManagerConfigurationProvider` loads secrets synchronously at `IConfiguration` build time. High Secrets Manager latency (cross-region call, cold cache) can breach the `initialDelaySeconds: 5` readiness probe.
**Action:** Increase `readinessProbe.initialDelaySeconds` to 15 temporarily. Check Secrets Manager endpoint latency in CloudWatch. Consider enabling Secrets Manager endpoint in VPC.

### CloudWatch Logs ingestion delay

**Symptom:** Logs visible in pods but not appearing in CloudWatch Insights.
**Cause:** Fluent Bit DaemonSet pod on the affected node is down or lagging.
**Action:**
```bash
kubectl get pods -n amazon-cloudwatch -l k8s-app=fluent-bit
kubectl logs -n amazon-cloudwatch <fluent-bit-pod> --tail=50
kubectl rollout restart daemonset/fluent-bit -n amazon-cloudwatch
```

### JWT validation failure in downstream services

**Symptom:** Downstream services return 401 after a rotation or deployment.
**Cause:** New JWT private key or issuer/audience mismatch.
**Action:** Verify `Jwt__Issuer` and `Jwt__Audience` match the values configured in consuming services. Check the public key endpoint if Cognito JWKS is being used.

---

## 6. Escalation

| Severity | Condition | Action |
|---|---|---|
| P1 | Availability < 99.5% for > 5 min | Page on-call immediately via `#rentifyx-oncall` |
| P2 | 5xx error rate > 1% for > 10 min | Page on-call |
| P3 | p99 latency > 500 ms for > 15 min | Notify on-call; investigate async |
| P4 | Single non-critical alert | Create ticket; no page |

---

## 7. CloudWatch Dashboard

**Dashboard name:** `rentifyx-identity`

URL: `https://console.aws.amazon.com/cloudwatch/home?region=us-east-1#dashboards:name=rentifyx-identity`

Key widgets to check during an incident:
- **Request rate (RPM)** — establish baseline before/after deploy
- **5xx error rate %** — primary SLO indicator
- **p99 latency** — SLO-02 and SLO-04
- **DynamoDB consumed capacity** — rule out DB throttling
- **Fluent Bit buffer size** — detect log lag

---

## 8. Terraform State

Remote state is stored in S3 bucket `rentifyx-tfstate`, key `identity-api/terraform.tfstate`, lock table `rentifyx-tflock`.

```bash
# View current state
cd iac/terraform
terraform init
terraform state list

# Force-unlock if a plan/apply left a stale lock
terraform force-unlock <LOCK_ID>
```

Never run `terraform apply` without peer review and a green `terraform plan` output committed to the PR.
