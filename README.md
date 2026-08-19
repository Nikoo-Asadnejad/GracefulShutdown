

A lightweight .NET library for safely shutting down applications while allowing **critical operations** to complete.

## Overview

When an application receives `SIGTERM`, the library:

1. Starts **draining** the application.

2. Changes the **readiness health check to `503`**, preventing new traffic.

3. Keeps the **liveness health check at `200`** so the application remains alive.

4. Waits for all registered critical operations to finish.

5. Shuts down the application after the last critical operation completes.

```text

                    SIGTERM

                       │

                       ▼

              ┌─────────────────┐

              │ Start Draining  │

              └────────┬────────┘

                       │

              Readiness = 503

              Liveness  = 200

                       │

                       ▼

                No new traffic

                       │

                       ▼

          Wait for critical operations

                       │

                       ▼

              Last operation ends

                       │

                       ▼

             Application shutdown

```

## Critical Operation Tracker

Use the `ICriticalOperationTracker` to register operations that must not be interrupted during shutdown.

```csharp

public async Task ProcessOrderAsync(

    CancellationToken cancellationToken)

{

    using var operation = _tracker.BeginOperation();

    await ProcessOrderInternalAsync();

}

```

The operation is automatically released when the `using` scope ends, including when an exception occurs.

## Shutdown Coordination

The tracker uses a `TaskCompletionSource` to signal when all critical operations have completed.

```text

Operation A ────────────────┐

Operation B ────────┐       │

Operation C ────┐   │       │

                │   │       │

              SIGTERM        │

                │            │

                ▼            ▼

             Draining

                │

                ▼

        Wait for all operations

                │

                ▼

          TaskCompletionSource

                │

                ▼

            Shutdown

```

There is no polling; shutdown waits asynchronously until the tracker is completed.

## Health Checks

The application should expose separate readiness and liveness endpoints.

| State | Readiness | Liveness |

|---|---:|---:|

| Normal | `200` | `200` |

| Draining | `503` | `200` |

**Why?**

- **Readiness `503`** tells Kubernetes/load balancers to stop sending new requests.

- **Liveness `200`** keeps the process alive while critical operations finish.

## Kubernetes

Example probes:

```yaml

livenessProbe:

  httpGet:

    path: /health/live

    port: 8080

readinessProbe:

  httpGet:

    path: /health/ready

    port: 8080

```

Configure a suitable termination grace period:

```yaml

spec:

  terminationGracePeriodSeconds: 60

```

The value should be based on the maximum expected duration of your critical operations.

## Recommended Usage

Track only operations that are genuinely business-critical, such as:

- Payment processing

- Booking/order confirmation

- Important external API calls

- Publishing critical events

- Completing transactional workflows

Normal read operations generally do not need to be tracked.

## Key Design

```text

SIGTERM

   │

   ▼

Start draining

   │

   ├── Readiness → 503

   └── Liveness  → 200

   │

   ▼

Wait for critical operations

   │

   ▼

All operations completed

   │

   ▼

Shutdown application

```

This allows the application to stop receiving new traffic without abruptly terminating important business operations.

"""

