# Security policy

## Reporting a vulnerability

Please do not report exploitable vulnerabilities in a public issue. Contact the repository owner privately through the security/contact mechanism configured on the GitHub repository. If no private contact is configured yet, enable GitHub Security Advisories or add a private reporting address before publishing the project broadly.

When reporting, include:

- a concise description and potential impact;
- affected version/commit and platform;
- minimal reproduction steps or a proof of concept that does not expose real secrets;
- any suggested mitigation, if known.

Please allow time to investigate and coordinate a fix before public disclosure.

## Security considerations for users

- **Review generated bundles.** They may contain proprietary code, configuration values, internal endpoints, or secrets from files that were included.
- **Treat filtering as defense in depth.** Built-in sensitive-file exclusions and `.gitignore` are useful safeguards, not a guarantee that every secret is detected.
- **Use `--allow-dangerous-files` sparingly.** It explicitly allows files normally excluded because their names or extensions look sensitive.
- **Be careful with remote inputs.** Only process repository URLs you trust and review the resulting artifact before sharing it.
- **Protect output files.** Store bundles with the same care as the source project; delete them when no longer needed.

## Supported versions

Security fixes are expected to target the latest maintained version. Until a formal release/support policy is established, users should prefer the latest release and report the exact version or commit they are using.
