using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace X2ModCompiler.Utilities;

/// <summary>
/// Synchronizes an XCOM 2 project file (.x2proj) with the actual files and folders present on the disk.
/// This utility ensures that the project structure in the IDE matches the physical directory structure,
/// which is critical for consistent builds and source control management.
/// </summary>
public class ProjectSynchronizer
{
    private readonly ILogger<ProjectSynchronizer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectSynchronizer"/> class.
    /// </summary>
    /// <param name="logger">The logger for synchronization diagnostics.</param>
    public ProjectSynchronizer(ILogger<ProjectSynchronizer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Regenerates the 'ItemGroup' sections of the specified .x2proj file based on a recursive disk scan.
    /// Automatically excludes internal build artifacts, script folders, and version control directories.
    /// </summary>
    /// <param name="x2projPath">The absolute path to the .x2proj file to synchronize.</param>
    public virtual void Synchronize(string x2projPath)
    {
        if (!File.Exists(x2projPath))
        {
            _logger.ZLogInformation($"Project file {x2projPath} not found. Skipping synchronization.");
            return;
        }

        var projectRoot = Path.GetDirectoryName(x2projPath)!;
        var x2projFilename = Path.GetFileName(x2projPath);

        XDocument doc = XDocument.Load(x2projPath);
        XNamespace ns = doc.Root!.GetDefaultNamespace();

        // Remove all existing ItemGroups that contain Folder, Content, or None
        _logger.ZLogInformation($"Clearing old ItemGroups...");
        var itemGroupsToRemove = doc.Descendants(ns + "ItemGroup")
            .Where(ig => ig.Elements(ns + "Folder").Any() ||
                         ig.Elements(ns + "Content").Any() ||
                         ig.Elements(ns + "None").Any())
            .ToList();

        foreach (var ig in itemGroupsToRemove)
        {
            foreach (var child in ig.Nodes())
            {
                if (child is XElement element)
                {
                    var includeValue = element.Attribute("Include")?.Value;
                    if (includeValue != null)
                    {
                        var elementType = element.Name.LocalName;
                        _logger.ZLogInformation($"Removed {elementType} '{includeValue}' from ItemGroups");
                    }
                }
            }
            ig.Remove();
        }

        _logger.ZLogInformation($"Scanning project directory for files and folders...");

        // Gather all files recursively
        var allFilesList = new List<string>();
        foreach (var file in Directory.GetFiles(projectRoot, "*.*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(projectRoot, file);
            if (!relativePath.Equals(x2projFilename, StringComparison.OrdinalIgnoreCase) &&
                !relativePath.StartsWith("ContentForCook", StringComparison.OrdinalIgnoreCase) &&
                !relativePath.StartsWith("BuildCache", StringComparison.OrdinalIgnoreCase) &&
                !relativePath.StartsWith(".scripts", StringComparison.OrdinalIgnoreCase) &&
                !relativePath.StartsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                allFilesList.Add(relativePath);
            }
        }

        var allFiles = allFilesList.ToArray();

        // Gather all unique folders
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in allFiles)
        {
            var dir = Path.GetDirectoryName(file);
            while (!string.IsNullOrEmpty(dir))
            {
                folders.Add(dir);
                dir = Path.GetDirectoryName(dir);
            }
        }

        var fileCount = allFiles.Length;
        var folderCount = folders.Count;
        _logger.ZLogInformation($"Found {fileCount} files and {folderCount} folders.");

        var newItemGroup = new XElement(ns + "ItemGroup");

        // Add Folder elements (sorted) - use standard LINQ for HashSet (small collection)
        foreach (var folder in folders.ToList().OrderBy(f => f))
        {
            newItemGroup.Add(new XElement(ns + "Folder", new XAttribute("Include", folder)));
        }

        // Add Content elements (sorted)
        foreach (var file in allFiles.OrderBy(f => f))
        {
            newItemGroup.Add(new XElement(ns + "Content", new XAttribute("Include", file)));
        }

        _logger.ZLogInformation($"Updating ItemGroup with folders and files...");

        // Sort nodes first by Name ascending (Folder before Content), then by Include ascending
        var sortedNodes = newItemGroup.Nodes().OfType<XElement>()
            .OrderBy(n => n.Name.LocalName)
            .ThenBy(n => n.Attribute("Include")?.Value)
            .ToList();

        foreach (var node in sortedNodes)
        {
            var includeValue = node.Attribute("Include")?.Value;
            var elementType = node.Name.LocalName;
            _logger.ZLogInformation($"Added {elementType} '{includeValue}' to ItemGroups");
        }

        doc.Root.Add(newItemGroup);
        doc.Save(x2projPath);

        _logger.ZLogInformation($"ItemGroup regeneration completed successfully for project {Path.GetFileNameWithoutExtension(x2projPath)}.");
    }
}
