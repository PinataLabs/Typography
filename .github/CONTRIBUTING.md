# How to contribute to PinataLabs.Typography

## Did you find a bug?

- Make sure that nobody has reported the bug before. Search the [issues](https://github.com/PinataLabs/Typography/issues) on GitHub.
- If you do not find an open issue for the problem, [open a new one](https://github.com/PinataLabs/Typography/issues/new/choose). Include a **title, the version you use, a clear description**, and a **code sample** or a **test case** that shows the expected behavior. Do not reuse an existing issue for a different problem.

## Did you write a patch that fixes a bug?

- Open a pull request on [PinataLabs/Typography](https://github.com/PinataLabs/Typography/pulls) with the patch.
- In the pull request description, describe the problem and the solution. Include the issue number if there is one.
- Make sure that your code follows the coding patterns in the repository. The build enforces StyleCop and `.editorconfig` rules, and it treats warnings as errors.
- Add or update tests for your change, and make sure that `dotnet test PinataLabs.Typography.slnx` passes.
- New `.cs` files start with this header:

  ```csharp
  // Copyright (c) PinataLabs.
  // Licensed under the Apache License, Version 2.0.
  ```

  Do not remove the upstream copyright line from files that already have it. See [NOTICE](../NOTICE).

## Do you want to add a feature or change the public API?

- Open an [issue](https://github.com/PinataLabs/Typography/issues/new/choose) that describes the change before you write the code. A maintainer must approve each public API addition or breaking change.

## Do you have a question about the library or the source code?

- Open an [issue](https://github.com/PinataLabs/Typography/issues) with your question.

## Code of Conduct

This project follows the [Code of Conduct](../CODE_OF_CONDUCT.md).

Thank you for your help!
