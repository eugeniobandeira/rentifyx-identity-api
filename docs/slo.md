# Service Level Objectives — RentifyX Identity API

## Overview

SLOs define the reliability targets the Identity API must meet in production. Each SLO has an associated error budget: the allowable amount of unreliability within the measurement window. Breaching 50% of the error budget in a 1-hour window triggers a page to on-call.

**Measurement window:** 30-day rolling for availability; 1-hour rolling for latency and error rate.

---

## SLO Definitions

| # | Objective | Target | Window | Error budget (30 days) |
|---|---|---|---|---|
| SLO-01 | Availability (uptime) | ≥ 99.9% | 30-day rolling | 43.8 min downtime |
| SLO-02 | p99 latency — all endpoints | < 500 ms | 1-hour rolling | — |
| SLO-03 | HTTP 5xx error rate | < 0.1% | 1-hour rolling | — |
| SLO-04 | p99 latency — auth endpoints | < 300 ms | 1-hour rolling | — |

---

## SLO-01 — Availability

**Target:** 99.9% of HTTP requests return a non-5xx response within a 30-day window.

**Measurement:**
- CloudWatch metric: `AWS/ApplicationELB` → `HTTPCode_Target_5XX_Count` / `RequestCount`
- Or: custom OTel metric `http.server.request.duration` filtered by `http.response.status_code >= 500`

**Error budget:** 43.8 minutes of total downtime per 30 days.

**Alert threshold:** Page when error budget burn rate exceeds 2× in a 1-hour window (fast burn) or 1× sustained over 6 hours (slow burn).

---

## SLO-02 — p99 Latency (All Endpoints)

**Target:** 99th percentile of request duration < 500 ms, measured over a 1-hour rolling window.

**Measurement:**
- OTel span: `http.server.request.duration` histogram, p99 aggregation per hour
- CloudWatch Insights query on the `/rentifyx/identity-api` log group:
  ```
  filter @type = "REQUEST"
  | stats pct(@duration, 99) as p99 by bin(1h)
  ```

**Alert threshold:** Page when p99 exceeds 400 ms for 3 consecutive 5-minute evaluation periods (warning at 80% budget).

---

## SLO-03 — HTTP 5xx Error Rate

**Target:** Fewer than 0.1% of requests result in a 5xx response, measured over a 1-hour rolling window.

**Measurement:**
- OTel metric: `http.server.request.duration` with attribute filter `http.response.status_code >= 500`
- Rate = `5xx_count / total_request_count` per hour

**Alert threshold:** Page when error rate exceeds 0.05% for 2 consecutive 5-minute periods.

**Exclusions:** Health check endpoints (`/health/ready`, `/health/live`) are excluded from error rate calculation.

---

## SLO-04 — p99 Latency (Auth Endpoints)

**Target:** 99th percentile of auth endpoint request duration < 300 ms, measured over a 1-hour rolling window.

**Auth endpoints in scope:**
- `POST /api/v1/auth/register`
- `POST /api/v1/auth/login`
- `POST /api/v1/auth/verify-email`
- `POST /api/v1/auth/reset-password`

**Measurement:**
- OTel span: `http.server.request.duration` filtered by `http.route` matching auth paths
- CloudWatch dashboard widget: p99 per route, 5-minute granularity

**Alert threshold:** Page when auth p99 exceeds 250 ms for 3 consecutive 5-minute periods.

---

## Error Budget Policy

| Burn rate | Action |
|---|---|
| > 2× in 1 hour | Page on-call immediately |
| > 1× sustained for 6 hours | Page on-call, open incident |
| Budget 50% consumed in first 15 days | Engineering review, freeze non-critical deploys |
| Budget exhausted | Freeze all deploys except reliability fixes |

---

## Log Shipping Architecture

The API writes structured JSON logs to stdout. A Fluent Bit DaemonSet (deployed via the [AWS for Fluent Bit](https://github.com/aws/aws-for-fluent-bit) Helm chart) ships logs from each node to CloudWatch Logs under the `/rentifyx/identity-api` log group.

```
Pod stdout
  └── Fluent Bit (DaemonSet, node-level)
        └── CloudWatch Logs /rentifyx/identity-api
              └── CloudWatch Insights (ad-hoc queries)
              └── CloudWatch Alarms (SLO breach alerts)
```

No AWS SDK dependency is required in the application for log shipping. Structured JSON (via `RenderedCompactJsonFormatter`) ensures Fluent Bit parses fields correctly.

---

## CloudWatch Dashboard

Dashboard name: `rentifyx-identity`

URL pattern: `https://console.aws.amazon.com/cloudwatch/home?region=us-east-1#dashboards:name=rentifyx-identity`

Widgets:
1. Request rate (RPM) — last 3 hours
2. 5xx error rate % — last 3 hours
3. p50 / p95 / p99 latency — last 3 hours
4. Auth endpoint p99 — last 3 hours
5. Error budget burn rate — 30-day rolling
6. DynamoDB consumed capacity — last 3 hours
