# Third-party software

## mihomo v1.19.32

Copyright: MetaCubeX and mihomo contributors.
License: GNU General Public License v3.0. See `third-party/LICENSE-mihomo.txt`.

Unmodified upstream binary, redistributed as a separate executable:

- `core/mihomo.exe`
- Original asset: `mihomo-windows-amd64-v1-go120-v1.19.32.zip`
- Source: https://github.com/MetaCubeX/mihomo/releases/tag/v1.19.32
- Original archive SHA-256: `f4f08cb9ff0f6315e2ab9e9c5dbb6ff703225d1e5f3ed4cb81b34f256939a697`
- The downloaded archive hash matched the SHA-256 digest in the official release API.

The corresponding upstream source for the pinned tag, including build definitions and dependency manifests, is included in `third-party/mihomo-v1.19.32-source.tar.gz`. Upstream source and build instructions are also available at https://github.com/MetaCubeX/mihomo/tree/v1.19.32 .

Aurora Proxy communicates with this executable through its process interface and local REST API. It does not modify or link the mihomo code into the client assembly. The client license does not supersede mihomo's license.
