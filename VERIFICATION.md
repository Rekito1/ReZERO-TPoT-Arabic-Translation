# v2.2 verification report

| Artifact | SHA-256 |
| --- | --- |
| v2.2 final GUI EXE, recorded at original build | `e5c8bf1bd5b5b74e58ffcdce6a010a52b50feaa1606e7a053708d54044c37b7a` |
| Fresh GUI rebuild with retained v2.2 payload and icon | `1c8754af3dc4b2bcac0d2c16e8375685edab24ee21f935de4d2c27fa6fe944f0` |
| Second GUI rebuild with identical source and inputs | `23ff9a3702e155c25c4570f1c49acdaf00d9af42c26d5f067597677067d2e5be` |
| Retained historical v2.2 console test EXE | `73d462c4758e4fee0f3c9a0eb2753bf3c382a4e32062e2e498685bbe15f25687` |
| Retained and embedded v2.2 payload ZIP | `c9d9db4725c56512402721edcdb638420a38ee528b5e6cd199b4d4d9ea4e752d` |
| Regenerated payload ZIP | `5b98766f263e171bbac3a71dc26c733bf82388023516144139f47ef6fa3493cf` |

The two fresh GUI builds each contain 87,552,000 bytes, the same as the historical release record. Their binary differences were confined to the PE timestamp and the generated .NET module version ID (MVID). The first rebuild has MVID `e2d5d7b8-c068-4f47-995b-ebebf4b6754b`. The second has MVID `5c8525b2-1af7-473d-8af6-cff48a2c0a93`. This demonstrates nondeterminism in the original build procedure. The final release EXE was absent from the local delivery folder and the Nexus-hosted binary was unavailable for direct inspection in this review. Its hash above comes from the saved release checksum. The observed nondeterminism explains why a fresh build can differ, but the exact differences against the released EXE remain unverified.

The two GUI rebuilds and retained console test build matched on the inspected managed entry point, reference assemblies, resource names, payload hash, and method records across nine types and 36 methods. Method records include IL, signatures, attributes, locals, and exception handlers. The console test build deliberately uses a different PE target and omits the native GUI manifest and icon. The release source and native manifest were copied unchanged. The icon extracted from a GUI rebuild matched the original icon file by SHA-256.

Using the published path-parameterized generator with the recorded original and localized inputs, all 369 ZIP members appeared in the same order and had identical uncompressed bytes, including every RZP1 stream and `plan.json`. The 368 generated members had new ZIP timestamps. The rebuild used the retained ZIP rather than the regenerated one. A newly compiled console copy of the source successfully installed and restored the patch on an isolated game copy. The installer verified the applied resources and confirmed the original archive, executable, and movie hashes after restoration.

The evidence supports functional equivalence to the retained v2.2 build inputs and historical test build. A clean public checkout can audit the source but needs the private payload and icon to rebuild the shipping package. A Nexus reviewer with the quarantined EXE can hash it, inspect its code and resources without launching it, extract its inputs locally, and repeat the comparison in [BUILD.md](BUILD.md). This report does not claim bit-for-bit identity with the hosted EXE or independent approval by Nexus Mods.
