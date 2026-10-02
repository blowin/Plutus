# Plutus

Plutus is a high-performance .NET 10 command-line interface (CLI) utility designed to bundle source code and project assets into a single structured Markdown document. It simplifies sharing source code repositories with Large Language Models (LLMs), generating project backups, and performing offline code reviews by capturing directory structure, file hierarchy, and text content in a consolidated format.

## Features

- **Automated Directory Tree Generation**: Produces a clean visual representation of the project hierarchy including indicators for ignored or oversized files.
- **Flexible Ignore System**: Integrates native support for standard `.gitignore` rules via `MAB.DotIgnore`, combined with built-in rules for common development junk files.
- **Security Protections**: Features a `DangerousFileIgnoreMatcher` that automatically blocks accidental collection of private keys, environment files, and credentials.
- **Smart Text Encoding**: Safely reads UTF-8, Windows-1251, and generic text encodings while actively detecting and ignoring binary data streams.
- **Configurable Restrictions**: Supports strict constraints on maximum individual file size to manage token consumption or storage space.

## Architecture

The project follows a clean, modular class library structure split across three distinct layers:

1. **Plutus.CLI**: Consists of user interaction components, CLI parameter parsing routines, and overall orchestrator logic.
2. **Plutus.Domain**: Contains the core business behavior including file discovery, human-readable size formatting, formatting rules, directory rendering tree logic, and individual domain matching policies.
3. **Plutus.Infrastructure**: Provides platform adapters, such as the `GitIgnoreMatcher` wrapping external dependency abstractions.

## Requirements

- .NET 10.0 SDK or higher
- Supported on Windows, macOS, and Linux operating systems

## Installation

Clone the repository and compile using the standard .NET CLI build pipeline:

```bash
git clone https://github.com/yourusername/Plutus.git
cd Plutus
dotnet build -c Release
```

## Usage

Run the compiled executable through the terminal while passing the path to the target project directory.

```bash
plutus <project-path> [options]
```

### Options

- `-o, --output <path>`: Specifies the exact output location for the generated file bundle. Defaults to `<folder-name>_bundle.md`.
- `--max-file-size <size>`: Sets the upper threshold for processing single files (e.g., `512K`, `1M`, `2MB`). Defaults to `1M`.
- `--no-gitignore`: Disables searching and parsing the root `.gitignore` configuration file.
- `--ignore <pattern>`: Inserts custom gitignore-style exclusion rules directly via parameter strings (can be repeated).
- `--no-default-excludes`: Disables default rules for common environment folder exclusions (`node_modules`, `bin`, `obj`).
- `--allow-dangerous-files`: Bypasses security rules preventing ingestion of sensitive files (`.env`, private keys).
- `-h, --help`: Displays reference guidance details for available commands.

### Examples

Bundle the current working directory with standard rules:
```bash
plutus .
```

Bundle a specific application with custom file constraints:
```bash
plutus C:\projects\MyApp --max-file-size 5M
```

Exclude specific assets and compile a direct layout to a temporary storage directory:
```bash
plutus . --ignore "vendor/" --ignore "*.min.js" -o /tmp/project_bundle.md
```

## Contributing

1. Fork the repository.
2. Create a specific feature branch (`git checkout -b feature/AmazingFeature`).
3. Commit modification sets (`git commit -m 'Add some AmazingFeature'`).
4. Push updates to the branch (`git push origin feature/AmazingFeature`).
5. Open a Pull Request for code review.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
