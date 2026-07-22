# Authoritative C++ Client Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an opt-in C++ TCP client that speaks the canonical Protobuf protocol to the C# authoritative server while leaving the legacy multiplayer transport unchanged by default.

**Architecture:** Keep the new client in `Source/network/authoritative` with a small synchronous request/response API. CMake generates `devilution.proto` C++ bindings only when `DEVILUTIONX_ENABLE_AUTHORITATIVE_CLIENT=ON`; the generated files remain in the build tree. The client owns the four-byte little-endian envelope framing and exposes handshake, command-batch, acknowledgement, and snapshot operations without depending on the legacy packet types.

**Tech Stack:** C++20, standalone Asio already vendored by the project, Protobuf C++ runtime/compiler, and the existing GTest/CMake test infrastructure.

---

### Task 1: Define the C++ wire boundary with tests

**Files:**
- Create: `Source/network/authoritative/envelope_codec.hpp`
- Create: `Source/network/authoritative/envelope_codec.cpp`
- Create: `test/authoritative_envelope_codec_test.cpp`
- Modify: `CMake/Tests.cmake`

- [ ] Write tests for a valid little-endian length-prefixed frame, fragmented reads, clean end-of-stream, truncated payloads, zero-length frames, and the one-megabyte maximum.
- [ ] Run the focused test and verify it fails because the codec does not exist.
- [ ] Implement bounded frame read/write helpers over `asio::ip::tcp::socket`.
- [ ] Run the focused test and verify it passes.

### Task 2: Add opt-in Protobuf generation

**Files:**
- Modify: `CMakeLists.txt`
- Modify: `CMake/Dependencies.cmake`
- Modify: `vcpkg.json`
- Modify: `protocol/README.md`

- [ ] Add `DEVILUTIONX_ENABLE_AUTHORITATIVE_CLIENT` defaulting to `OFF` and require Protobuf only when enabled.
- [ ] Generate `devilution.pb.h` and `devilution.pb.cc` from `protocol/devilution.proto` into the build tree.
- [ ] Add a vcpkg feature that supplies the optional Protobuf dependency without changing the legacy dependency set.
- [ ] Document the opt-in configure command and generated-binding ownership.

### Task 3: Implement the opt-in authoritative client

**Files:**
- Create: `Source/network/authoritative/authoritative_client.hpp`
- Create: `Source/network/authoritative/authoritative_client.cpp`
- Create: `test/authoritative_client_test.cpp`
- Modify: `Source/CMakeLists.txt`
- Modify: `CMake/Tests.cmake`

- [ ] Write a loopback-server test that verifies the client sends `ClientHello`, submits one `CommandBatch`, returns `CommandAck`, and consumes a following `Snapshot`.
- [ ] Run the feature-enabled test and verify it fails before the client exists.
- [ ] Implement connect, handshake validation, command submission, snapshot reception, and deterministic disconnect/error handling.
- [ ] Keep the client out of `abstract_net::MakeNet`; integration with gameplay remains a later vertical slice.
- [ ] Run the feature-enabled test and verify it passes.

### Task 4: Update status and verify

**Files:**
- Modify: `docs/csharp-authoritative-server-migration.md`
- Modify: `protocol/README.md`
- Modify: `docs/CHANGELOG.md` if present

- [ ] Mark C++ generation and the minimal client as complete, leaving gameplay adapter, full resync, and content-hash parity as remaining gates.
- [ ] Run the default build/test configuration to prove legacy behavior is unchanged.
- [ ] Run the opt-in authoritative-client configuration and focused GTests when Protobuf is available; record any environment limitation explicitly.

