using Microsoft.Extensions.Logging;

namespace Acl.Fs.Core.Interfaces;

internal interface IStreamProvider
{
    System.IO.Stream CreateInput(string sourcePath, ILogger logger);
    System.IO.Stream CreateOutput(string destinationPath, ILogger logger);
}