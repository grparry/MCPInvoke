# Contributing to MCPInvoke

We welcome contributions to MCPInvoke! This document provides guidelines for contributing to the project.

## Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally
3. **Create a feature branch** from `main`
4. **Make your changes** following our guidelines
5. **Test your changes** thoroughly
6. **Submit a pull request**

## Development Setup

### Prerequisites

- .NET 6.0 or later
- Git

### Building the Project

```bash
git clone https://github.com/grparry/MCPInvoke.git
cd MCPInvoke
dotnet build
```

### Running Tests

```bash
dotnet test
```

The project includes comprehensive test coverage. Please ensure all tests pass before submitting a pull request.

## Contribution Guidelines

### Code Style

- Follow C# coding conventions and .NET naming guidelines
- Use meaningful variable and method names
- Add XML documentation for public APIs
- Keep methods focused and concise

### Testing

- Write unit tests for new functionality
- Ensure all existing tests continue to pass
- Test coverage should be maintained or improved
- Include integration tests for complex scenarios

### Documentation

- Update README.md if adding new features
- Add XML documentation comments for public APIs
- Update CHANGELOG.md with your changes
- Include examples for new functionality

## Types of Contributions

### Bug Reports

When reporting bugs, please include:
- Clear description of the issue
- Steps to reproduce
- Expected vs actual behavior
- Environment details (.NET version, OS, etc.)
- Sample code if applicable

### Feature Requests

For new features, please:
- Check if the feature already exists
- Describe the use case and benefits
- Provide implementation ideas if possible
- Consider backward compatibility

### Code Contributions

We welcome:
- Bug fixes
- Performance improvements
- New features (after discussion)
- Documentation improvements
- Test improvements

## Pull Request Process

1. **Create a feature branch** with a descriptive name
2. **Write clear commit messages** describing your changes
3. **Include tests** for new functionality
4. **Update documentation** as needed
5. **Ensure CI passes** before requesting review
6. **Respond to feedback** promptly

### Commit Message Format

Use conventional commit messages:
```
type(scope): description

Examples:
feat(execution): add support for complex object parameters
fix(middleware): resolve content schema compliance issue
docs(readme): update installation instructions
test(execution): add integration tests for workflow scenarios
```

## Code of Conduct

This project follows a standard code of conduct. Please be respectful and constructive in all interactions.

## Questions?

If you have questions about contributing:
- Open an issue for discussion
- Check existing issues and pull requests
- Review the documentation

Thank you for contributing to MCPInvoke!