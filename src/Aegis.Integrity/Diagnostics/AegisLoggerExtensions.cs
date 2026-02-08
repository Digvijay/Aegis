
using Microsoft.Extensions.Logging;
using System;

namespace Aegis.Integrity.Diagnostics
{
    /// <summary>
    /// High-performance logging extensions using source generators.
    /// </summary>
    public static partial class AegisLoggerExtensions
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "Discovery started for page {PageNumber}. Atom count: {AtomCount}")]
        public static partial void DiscoveryStarted(this ILogger logger, int pageNumber, int atomCount);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "Structure detected: {StructureType} from atom {StartAtom} to {EndAtom}")]
        public static partial void StructureDetected(this ILogger logger, string structureType, int startAtom, int endAtom);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Debug,
            Message = "Generated chunk {ChunkIndex} with {TokenCount} tokens. Boundary reason: {Reason}")]
        public static partial void ChunkGenerated(this ILogger logger, int chunkIndex, int tokenCount, string reason);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Warning,
            Message = "Backpressure applied at atom {AtomIndex}. Collision with {StructureType}. Action: {Action}")]
        public static partial void BackpressureApplied(this ILogger logger, int atomIndex, string structureType, string action);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Information,
            Message = "Document mapping complete. Pages: {PageCount}, Atoms: {AtomCount}, Structures: {StructureCount}")]
        public static partial void MappingComplete(this ILogger logger, int pageCount, int atomCount, int structureCount);
    }
}
