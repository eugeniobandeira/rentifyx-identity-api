# ADR-008: Kubernetes deployment strategy (RollingUpdate)

- **Date:** 2026-06-21
- **Status:** Accepted

## Context

The identity service runs on AWS EKS and must be deployable without downtime. New releases are shipped via GitHub Actions. We need to choose a deployment strategy that balances zero-downtime guarantees, rollback speed, and operational simplicity.

## Options Considered

- **Option A — Recreate**: Terminate all old pods, then start new ones. Simple, but causes downtime. Unacceptable for an identity service (login would fail during deployment).
- **Option B — RollingUpdate**: Gradually replace old pods with new ones. Zero downtime if `minReadySeconds` and readiness probes are configured correctly. Native to Kubernetes; no additional tooling required.
- **Option C — Blue/Green**: Maintain two identical environments; switch traffic at the load balancer. Zero downtime and instant rollback. But requires double the compute resources and more complex traffic management (ALB weighted target groups or Istio).
- **Option D — Canary**: Route a percentage of traffic to the new version. Ideal for gradual rollout with real-traffic validation. Requires a service mesh or Argo Rollouts — additional operational complexity at this stage.

## Decision

**Option B** — `RollingUpdate` with the following parameters:

```yaml
strategy:
  type: RollingUpdate
  rollingUpdate:
    maxSurge: 1        # allow 1 extra pod during rollout
    maxUnavailable: 0  # never reduce below the desired replica count
```

Supporting configuration:
- **HPA**: `minReplicas: 2`, `maxReplicas: 10` — at least 2 pods always running ensures a new pod is healthy before an old one terminates.
- **Readiness probe**: `GET /health/ready` — a pod only receives traffic after it passes this probe (Secrets Manager connected, DynamoDB reachable).
- **Liveness probe**: `GET /health/live` — restarts pods that are deadlocked without restarting healthy pods.
- **PodDisruptionBudget**: `minAvailable: 1` — ensures at least one pod stays up during voluntary disruptions (node drain, cluster upgrade).
- **Resources**: `requests: {memory: 256Mi, cpu: 100m}`, `limits: {memory: 512Mi, cpu: 500m}`.

Rollback: `kubectl rollout undo deployment/rentifyx-identity-api` reverts to the previous ReplicaSet within seconds.

## Consequences

**Easier:**
- Zero-downtime deploys with no additional tooling (pure Kubernetes).
- Rollback is instant via `kubectl rollout undo`.
- `maxUnavailable: 0` guarantees the cluster absorbs full production traffic throughout the rollout.

**Harder:**
- During a rollout, both the old and new version run simultaneously. The API must be backward-compatible (no breaking schema changes in DynamoDB or JWT claims without versioning).
- A bad deployment that passes readiness probes but has a subtle bug will fully replace the fleet before the issue is detected. SLO alerting (error rate > 1% for 5 min) is the safety net.
- Blue/Green or Canary should be reconsidered when the service reaches higher traffic volumes where partial rollout risk assessment becomes critical.
