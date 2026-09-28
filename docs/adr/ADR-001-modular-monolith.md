# ADR-001: Modular monolith over microservices

**Status:** Accepted
**Date:** 2026-09-07

## Context

Quizware runs one quiz tournament, in one hall, on one venue server, for one
operator console and a handful of display screens at a time. There is no
independent scaling requirement, no separate team per subsystem, and no need
to deploy parts of the system independently of each other.

## Decision

Build Quizware as a **modular monolith**: one deployable ASP.NET Core Web API
process, internally divided into well-bounded modules (Domain, Application,
Infrastructure, Api, and an optional Buzzer module) with enforced dependency
rules between them (see ADR-010 / the module boundary map).

## Alternatives rejected

**Microservices.** Splitting the system into independently deployable
services (e.g. a scoring service, a question-bank service, a buzzer service)
was rejected as unnecessary complexity for the actual scale of the problem:
one hall, one event at a time, one small team. Microservices would add
network calls, distributed-transaction concerns, and operational overhead
(service discovery, independent deployments, cross-service tracing) with no
corresponding benefit — nothing about Quizware's load or team structure
justifies the cost.

## Consequences

- Internal module boundaries are enforced by convention and architecture
  tests (NetArchTest — see `P3-02`), not by network boundaries or separate
  deployments.
- A future split into services remains possible if the scale or team
  structure ever changes, because the module boundaries already exist inside
  the monolith — but it is not designed for today, and should not be built
  for speculatively.
