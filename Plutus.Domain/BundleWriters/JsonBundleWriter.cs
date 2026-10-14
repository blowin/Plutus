using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain.Minifier;
using Plutus.Domain.Tokens;
using Plutus.Domain.Tokens.Applier;

namespace Plutus.Domain.BundleWriters;

public class JsonBundleWriter(
    IFileInfoDetailProvider fileInfoDetailProvider,
    IMarkdownLanguageProvider markdownLanguageProvider,
    ICodeMinifier codeMinifier,
    System.Text.Encoding encoding,
    ITokenBudgetApplier tokenBudgetApplier)
    : BaseBundleWriter(fileInfoDetailProvider, markdownLanguageProvider, codeMinifier, encoding, tokenBudgetApplier)
{
    public override string FormatExtension => "json";

    protected override async Task WriteContentAsync(List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)> roots,
        IFileInfo output,
        List<ProcessedFileContext> files,
        FileSize maxFileSize,
        TokenBudgetSummary? tokenBudgetSummary)
    {
        var includedFiles = files.Where(x => !x.IsSkipped && x.Entry.File.Length <= maxFileSize).ToList();
        var jsonModel = new JsonBundleModel
        {
            Metadata = new BundleMetadata
            {
                TotalFiles = includedFiles.Count,
                TotalSizeInBytes = includedFiles.Sum(x => x.Entry.File.Length),
                MaxFileSizeLimit = maxFileSize,
                TokenEstimateMethod = tokenBudgetSummary?.EstimatorMethod,
                EstimatedSourceTokens = tokenBudgetSummary?.CandidateTokens,
                SelectedSourceTokens = tokenBudgetSummary?.SelectedTokens,
                MaxTokens = tokenBudgetSummary?.MaxTokens,
                TokenOverflowPolicy = tokenBudgetSummary?.OverflowPolicy.ToString().ToLowerInvariant(),
                TokenSelectionStrategy = tokenBudgetSummary?.SelectionStrategy.ToString()
            },
            Roots = roots.Select(r => MapDirectoryTree(r.ProjectNode)).ToList(),
            Files = files.Select(f => new JsonFileModel
            {
                RelativePath = f.Entry.RelativePath,
                SizeInBytes = f.Entry.File.Length,
                Language = f.Language,
                IsSkipped = f.IsSkipped,
                SkipReason = f.SkipReason,
                Content = f.Content,
                EstimatedTokens = f.EstimatedTokens
            }).ToList()
        };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var jsonString = JsonSerializer.Serialize(jsonModel, jsonOptions);
        await using var writer = FileInfoDetailProvider.CreateWriterForFile(output, Encoding);
        await writer.WriteAsync(jsonString).ConfigureAwait(false);
    }

    private JsonFolderNode MapDirectoryTree(ProjectNode node)
    {
        var folder = new JsonFolderNode
        {
            Name = node.Name,
            RelativePath = node.RelativePath,
            IsDirectory = node.IsDirectory,
            Status = node.Status.ToString()
        };

        foreach (var child in node.Children)
        {
            folder.Children.Add(MapDirectoryTree(child));
        }

        return folder;
    }

    private class JsonBundleModel
    {
        public BundleMetadata Metadata { get; set; } = new();
        public List<JsonFolderNode> Roots { get; set; } = [];
        public List<JsonFileModel> Files { get; set; } = [];
    }

    private class BundleMetadata
    {
        public int TotalFiles { get; set; }
        public long TotalSizeInBytes { get; set; }
        public long MaxFileSizeLimit { get; set; }
        public string? TokenEstimateMethod { get; set; }
        public long? EstimatedSourceTokens { get; set; }
        public long? SelectedSourceTokens { get; set; }
        public int? MaxTokens { get; set; }
        public string? TokenOverflowPolicy { get; set; }
        public string? TokenSelectionStrategy { get; set; }
    }

    private class JsonFolderNode
    {
        public string Name { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<JsonFolderNode> Children { get; set; } = [];
    }

    private class JsonFileModel
    {
        public string RelativePath { get; set; } = string.Empty;
        public long SizeInBytes { get; set; }
        public string Language { get; set; } = string.Empty;
        public bool IsSkipped { get; set; }
        public string? SkipReason { get; set; }
        public string? Content { get; set; }
        public int? EstimatedTokens { get; set; }
    }
}
