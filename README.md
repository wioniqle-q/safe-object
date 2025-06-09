# Acl.Fs

[<img src="https://img.shields.io/badge/Code%20Coverage-84%25-90EE90?logo=rider" alt="Code Coverage" style="width: 160px;">](https://github.com/user-attachments/assets/0b1d115a-cce8-429d-9476-abe0a360ae7d)
[![Tests](https://github.com/wioniqle-q/acl-lib/actions/workflows/test.yml/badge.svg?event=workflow_dispatch)](https://github.com/wioniqle-q/acl-lib/actions/workflows/test.yml)



A high-performance file encryption library for .NET 9 that provides cryptographic capabilities with
cross-platform optimizations.

## Overview

Acl.Fs implements cryptographic approaches AES-256-GCM encryption 
The library is designed for applications requiring performance-sensitive file encryption.

## Features

### Cryptographic Support

- **AES-256-GCM**
- **PBKDF2-SHA256**
- **HKDF-SHA256**

## Architecture

The library is organized into several core components:

- **Acl.Fs.Core**: Primary encryption/decryption services and cryptographic utilities
- **Acl.Fs.Stream**: Platform-optimized direct I/O implementations
- **Acl.Fs.Native**: Native library bindings and P/Invoke interfaces
- **Acl.Fs.Abstractions**: Common constants, and data models
- **Acl.Fs.Vault**: Key management services (in development) (As you wish you can add this later)

## Platform Optimizations

### Linux

- `posix_fadvise(POSIX_FADV_SEQUENTIAL)` 
- `posix_fadvise(POSIX_FADV_DONTNEED)` 
- `ioprio_set()` 
- `fsync()` 

### Windows

- `FlushFileBuffers()` 
- Overlapped I/O for asynchronous operations

### macOS

- `fcntl(F_FULLFSYNC)`

## Contributing

Contributions are welcome.

## Reporting Issues
Should you encounter any issues, please submit a new issue on the project repository, including a detailed description of your environment (e.g., operating system, version, hardware), the problem, steps to reproduce, and any relevant logs or error messages.
