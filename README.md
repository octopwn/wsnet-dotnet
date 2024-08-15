# WSNet Implementation in .NET
This project, referred to as the "proxy" in the documentation, is designed to provide the necessary WebSocket-to-TCP translation required for the tools running inside of OctoPwn. This enables network communication between various components.  
This is currently the only implementation which offers the authentication proxy feature.

## How It Works
Upon starting the application, a WebSocket server is set up on `localhost` at port `8700`. The server then waits for a connection from OctoPwn to facilitate the required communication.

## Features
- **WebSocket-to-TCP translation**: Facilitates communication between OctoPwn and other networked components.
- **Authentication Proxy**: Allows the in-browser OctoPwn to perform NTLM and Kerberos authentication using the current user's context.

## Support Matrix

| Feature                 | Windows | Linux | Mac |
|-------------------------|---------|-------|-----|
| TCP Client              | ✔       | ✘     | ✘   |
| TCP Server              | ✘       | ✘     | ✘   |
| UDP Client              | ✘       | ✘     | ✘   |
| UDP Server              | ✘       | ✘     | ✘   |
| Local File Browser      | ✘       | ✘     | ✘   |
| Authentication Proxy    | ✔       | ✘     | ✘   |

## Getting Started

This project is built using C# on the .NET Framework 4.7. We've chosen .NET 4.7 to maximize compatibility across all versions of Windows, ensuring that the application can run smoothly on a wide range of systems.

### Prerequisites

To compile this project, you'll need to install the following:

- **Visual Studio**: We recommend using Visual Studio 2019 or later, as it provides excellent support for .NET 4.7 projects.
- **.NET Framework 4.7 SDK**: Ensure that you have the .NET 4.7 SDK installed. You can download it directly from Microsoft's official website.

### Building the Project

Once you've installed Visual Studio and the .NET 4.7 SDK, follow these steps to compile the project:

1. Clone the repository to your local machine.
2. Open the solution file (`.sln`) in Visual Studio.
3. Build the solution by selecting `Build > Build Solution` from the top menu.

If everything is set up correctly, Visual Studio will compile the project and generate the necessary binaries.

### Running the Application

After successfully compiling the project, you'll find the executable file in the `bin\Release` or `bin\Debug` directory (depending on your build configuration). You can run the application directly from there.

### Download Precompiled Binaries

If you prefer not to compile the project yourself, you can download the precompiled binaries from the [Releases](https://github.com/octopwn/wsnet-dotnet/releases) section of this GitHub repository. Simply download the latest release, extract the files, and run the executable to start using the application.

