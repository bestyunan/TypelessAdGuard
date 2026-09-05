# Compatibility

Target: Windows 10/11 x64, .NET Framework 4.8+. x86 Windows, macOS and Linux are unsupported. Windows on ARM is not validated.

| Check | Evidence / limitation |
|---|---|
| Matcher | Positive and negative policy cases, two exact promotion variants |
| UIA closure | Synthetic exact, high-demand, six nested Custom wrappers, unrelated notice and ignored-close cases |
| Settings | Unicode/spaces, saved-path precedence, custom paths and ambiguous discovery |
| Real Typeless | Development machine, version 2.5.0; user reported the latest guard working well before portable packaging |
| Relocation | Test runs from a copied directory with spaces/Unicode and isolated user data |
| Read-only program folder | Optional `portable-smoke.ps1 -TestReadOnly` uses a temporary write-deny ACL and restores it |
| Different machine / clean VM | Not yet verified; relocation tests do not substitute for this |
| Other Typeless versions / languages | Not yet verified; unknown structures and copy are skipped |

Before a stable release, test on at least one clean Windows installation with no development tools and one other real Typeless installation. Check missing target, custom installation path, normal/elevated target, tray pause/resume, application restart and real dictation after dismissal. Do not claim those checks passed based only on unit tests or synthetic controls.

Runtime detection checks OS, architecture, .NET release and data-directory writability. It does not prove that a particular Typeless build supports the expected accessibility interfaces.
