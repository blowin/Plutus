using System.Text;
using System.Xml;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain.Minifier;
using Plutus.Domain.Tokens;
using Plutus.Domain.Tokens.Applier;

namespace Plutus.Domain.BundleWriters;

public class XmlBundleWriter(
    IFileInfoDetailProvider fileInfoDetailProvider,
    IMarkdownLanguageProvider markdownLanguageProvider,
    ICodeMinifier codeMinifier,
    Encoding encoding,
    ITokenBudgetApplier tokenBudgetApplier)
    : BaseBundleWriter(fileInfoDetailProvider, markdownLanguageProvider, codeMinifier, encoding, tokenBudgetApplier)
{
    public override string FormatExtension => "xml";

    protected override async Task WriteContentAsync(List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)> roots,
        IFileInfo output,
        List<ProcessedFileContext> files,
        FileSize maxFileSize,
        TokenBudgetSummary? tokenBudgetSummary)
    {
        var xmlSettings = new XmlWriterSettings
        {
            Async = true,
            Indent = true,
            Encoding = Encoding
        };

        await using var streamWriter = FileInfoDetailProvider.CreateWriterForFile(output, Encoding);
        using var xmlWriter = XmlWriter.Create(streamWriter, xmlSettings);

        await xmlWriter.WriteStartDocumentAsync();
        await xmlWriter.WriteStartElementAsync(null, "Bundle", null);

        // Metadata Aggregate Node
        await xmlWriter.WriteStartElementAsync(null, "Metadata", null);
        await xmlWriter.WriteElementStringAsync(null, "TotalFiles", null, files.Count.ToString());
        await xmlWriter.WriteElementStringAsync(null, "TotalSizeInBytes", null, files.Sum(x => x.Entry.File.Length).ToString());
        await xmlWriter.WriteElementStringAsync(null, "MaxFileSizeLimit", null, maxFileSize.ToString());
        if (tokenBudgetSummary is { } tokenSummary)
        {
            await xmlWriter.WriteElementStringAsync(null, "TokenEstimateMethod", null, tokenSummary.EstimatorMethod);
            await xmlWriter.WriteElementStringAsync(null, "EstimatedSourceTokens", null, tokenSummary.CandidateTokens.ToString());
            await xmlWriter.WriteElementStringAsync(null, "SelectedSourceTokens", null, tokenSummary.SelectedTokens.ToString());
            await xmlWriter.WriteElementStringAsync(null, "MaxTokens", null, tokenSummary.MaxTokens.ToString());
            await xmlWriter.WriteElementStringAsync(null, "TokenOverflowPolicy", null, tokenSummary.OverflowPolicy.ToString().ToLowerInvariant());
            await xmlWriter.WriteElementStringAsync(null, "TokenSelectionStrategy", null, tokenSummary.SelectionStrategy.ToCliValue());
        }
        await xmlWriter.WriteEndElementAsync();

        // Directory Mapping Structure
        await xmlWriter.WriteStartElementAsync(null, "Roots", null);
        foreach (var r in roots)
        {
            await WriteXmlNodeAsync(xmlWriter, r.ProjectNode);
        }
        await xmlWriter.WriteEndElementAsync();

        // Data Blocks Array
        await xmlWriter.WriteStartElementAsync(null, "Files", null);
        foreach (var file in files)
        {
            await xmlWriter.WriteStartElementAsync(null, "File", null);
            await xmlWriter.WriteAttributeStringAsync(null, "path", null, file.Entry.RelativePath);
            await xmlWriter.WriteAttributeStringAsync(null, "size", null, file.Entry.File.Length.ToString());
            await xmlWriter.WriteAttributeStringAsync(null, "language", null, file.Language);
            if (file.EstimatedTokens.HasValue)
            {
                await xmlWriter.WriteAttributeStringAsync(null, "estimatedTokens", null, file.EstimatedTokens.Value.ToString());
            }

            if (file.IsSkipped)
            {
                await xmlWriter.WriteAttributeStringAsync(null, "skipped", null, "true");
                await xmlWriter.WriteAttributeStringAsync(null, "reason", null, file.SkipReason);
            }
            else
            {
                await xmlWriter.WriteStartElementAsync(null, "Content", null);
                await xmlWriter.WriteCDataAsync(file.Content ?? string.Empty);
                await xmlWriter.WriteEndElementAsync();
            }
            await xmlWriter.WriteEndElementAsync();
        }
        await xmlWriter.WriteEndElementAsync();

        await xmlWriter.WriteEndElementAsync(); // End Bundle
        await xmlWriter.WriteEndDocumentAsync();
        await xmlWriter.FlushAsync();
    }

    private async Task WriteXmlNodeAsync(XmlWriter writer, ProjectNode node)
    {
        await writer.WriteStartElementAsync(null, node.IsDirectory ? "Directory" : "File", null);
        await writer.WriteAttributeStringAsync(null, "name", null, node.Name);
        await writer.WriteAttributeStringAsync(null, "status", null, node.Status.ToString());

        foreach (var child in node.Children)
        {
            await WriteXmlNodeAsync(writer, child);
        }
        await writer.WriteEndElementAsync();
    }
}
