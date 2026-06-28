#!/usr/bin/env bash
# LocalStack initialization script — runs on container startup via ready.d
# Creates the rentifyx-identity DynamoDB table, GSIs, TTL policy, and seeds Secrets Manager.
set -euo pipefail

REGION="sa-east-1"
TABLE="rentifyx-identity"
SECRET_NAME="rentifyx/identity/development"

echo "[init-localstack] Creating DynamoDB table: ${TABLE}"

awslocal dynamodb create-table \
  --region "${REGION}" \
  --table-name "${TABLE}" \
  --attribute-definitions \
    AttributeName=PK,AttributeType=S \
    AttributeName=GSI_Email_PK,AttributeType=S \
    AttributeName=GSI_TaxId_PK,AttributeType=S \
  --key-schema AttributeName=PK,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST \
  --global-secondary-indexes \
    '[
      {
        "IndexName": "GSI_Email",
        "KeySchema": [{"AttributeName": "GSI_Email_PK", "KeyType": "HASH"}],
        "Projection": {"ProjectionType": "ALL"}
      },
      {
        "IndexName": "GSI_TaxId",
        "KeySchema": [{"AttributeName": "GSI_TaxId_PK", "KeyType": "HASH"}],
        "Projection": {"ProjectionType": "ALL"}
      }
    ]'

echo "[init-localstack] Enabling TTL on attribute 'TTL'"
awslocal dynamodb update-time-to-live \
  --region "${REGION}" \
  --table-name "${TABLE}" \
  --time-to-live-specification "Enabled=true,AttributeName=TTL"

echo "[init-localstack] Seeding Secrets Manager secret: ${SECRET_NAME}"

HMAC_KEY=$(openssl rand -hex 32)
JWT_PRIVATE_KEY=$(openssl genrsa 2048 2>/dev/null)

SECRET_VALUE=$(printf '{"Hmac:Key":"%s","Jwt:PrivateKeyPem":"%s","Ses:FromAddress":"noreply@rentifyx.com.br"}' \
  "${HMAC_KEY}" \
  "$(echo "${JWT_PRIVATE_KEY}" | awk '{printf "%s\\n", $0}')")

awslocal secretsmanager create-secret \
  --region "${REGION}" \
  --name "${SECRET_NAME}" \
  --secret-string "${SECRET_VALUE}"

echo "[init-localstack] Initialization complete."
