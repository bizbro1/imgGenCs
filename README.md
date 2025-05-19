# OpenAI Image Generator

A desktop application that generates images using OpenAI's DALL-E API. Built with C# and WPF.

## Features

- Generate images from text prompts using OpenAI's DALL-E API
- Customize image settings (size, quality, format)
- Save generated images to your computer
- Maintain a history of your prompts
- Modern and user-friendly interface

## Requirements

- Windows 10 or later
- .NET 7.0 Runtime
- OpenAI API key

## Setup

1. Download and install the .NET 7.0 Runtime from [Microsoft's website](https://dotnet.microsoft.com/download/dotnet/7.0)
2. Download the latest release from the releases page
3. Extract the ZIP file to a location of your choice
4. Run `ImagenGenC.exe`
5. When prompted, enter your OpenAI API key (you can get one from [OpenAI's platform](https://platform.openai.com/))

## Usage

1. Enter your image prompt in the text box
2. Select desired image settings:
   - Size: 1024x1024 (square), 1024x1536 (portrait), or 1536x1024 (landscape)
   - Quality: Low, Medium, or High
   - Format: PNG, JPEG, or WebP
3. Click "Generate Image"
4. Once generated, you can:
   - Save the image using the "Save Image" button
   - Clear the current image and prompt using the "Clear" button
   - Select a previous prompt from the history panel

## Building from Source

1. Clone the repository
2. Open the solution in Visual Studio 2022
3. Restore NuGet packages
4. Build the solution
5. Run the application

## Dependencies

- OpenAI .NET SDK
- Microsoft.Extensions.Configuration.Json
- Microsoft.Extensions.DependencyInjection

## License

MIT License 