# Safe-Object

A secure file encryption and storage solution built with C#, emphasizing security, performance, and memory safety.

> [!NOTE]  
> This project has a separate UI project that includes a user-friendly interface for easier interaction. Please check out the UI project for enhanced features.
> [Click Me To Access UI Project](https://github.com/wioniqle-q/safe-object-avalonia)

## Features

- **Two-Layer Encryption**:
  - **Key Hierarchy**: Encrypted with a user-specific public master key (which is an AES-256 key derived via PBKDF2 with SHA3-512, Then this result is further encrypted with a system security key (also AES-256, derived via the same PBKDF2 process))

- **AES-GCM Encryption**:
  - **Algorithm**: Implements AES-256 in Galois/Counter Mode (GCM) for authenticated encryption with associated data (AEAD).
  - **Properties**: AES-GCM with a 256-bit key for encryption. Uses a 16-byte (128-bit) authentication tag per block.
  - **Nonce Management**: Derives unique nonces per block using an HKDF-based construction (HMAC-SHA256).

- **Secure Cryptography**:
  - `RFC 2898` key derivation with SHA3-512 for system security keys

- **Secure Stream Processing**:
  - Processes files in configurable-sized chunks to support large files
  - Implements per-block nonce derivation for stronger security
  
> [!IMPORTANT]  
> **Linux-Specific Optimizations**: It offers several key technical features:  
> - **I/O Priority Control**: Uses `ioprio_set` syscalls  
> - **Kernel Hints**: Implements `posix_fadvise` with `POSIX_FADV_SEQUENTIAL`  
> - **Memory Management**: Uses `POSIX_FADV_DONTNEED`

> [!IMPORTANT] 
> **Windows-Specific Optimizations**: It offers several key technical features:
> - **Direct Buffer Flushing:** Uses Windows-native `FlushFileBuffers` API

## Reporting Issues
Should you encounter any issues, please submit a new issue on the project repository, including a detailed description of your environment (e.g., operating system, version, hardware), the problem, steps to reproduce, and any relevant logs or error messages.
