using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace XCom2ModCompiler.Utilities;

public class ProjectSynchronizer
{
    private readonly ILogger<ProjectSynchronizer> _logger;

    public ProjectSynchronizer(ILogger<ProjectSynchronizer> logger)
    {
        _logger = logger;
    }

    public virtual void Synchronize(string x2projPath)
    {
        if (!File.Exists(x2projPath))
        {
            Console.WriteLine($"Project file {x2projPath} not found. Skipping synchronization.");
            return;
        }

        var projectRoot = Path.GetDirectoryName(x2projPath)!;
        var x2projFilename = Path.GetFileName(x2projPath);

        XDocument doc = XDocument.Load(x2projPath);
        XNamespace ns = doc.Root!.GetDefaultNamespace();

        // Remove all existing ItemGroups that contain Folder, Content, or None
        _logger.LogInformation("Clearing old ItemGroups...");
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
                        _logger.LogInformation($"Removed {element.Name.LocalName} '{includeValue}' from ItemGroups");
                    }
                }
            }
            ig.Remove();
        }

        _logger.LogInformation("Scanning project directory for files and folders...");

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
        _logger.LogInformation($"Found {fileCount} files and {folderCount} folders.");

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

        _logger.LogInformation("Updating ItemGroup with folders and files...");

        // Sort nodes first by Name ascending (Folder before Content), then by Include ascending
        var sortedNodes = newItemGroup.Nodes().OfType<XElement>()
            .OrderBy(n => n.Name.LocalName)
            .ThenBy(n => n.Attribute("Include")?.Value)
            .ToList();

        foreach (var node in sortedNodes)
        {
            var includeValue = node.Attribute("Include")?.Value;
            _logger.LogInformation($"Added {node.Name.LocalName} '{includeValue}' to ItemGroups");
        }

        doc.Root.Add(newItemGroup);
        doc.Save(x2projPath);

        _logger.LogInformation($"ItemGroup regeneration completed successfully for project '{Path.GetFileNameWithoutExtension(x2projPath)}'.");
    }
}
