#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;

namespace Game.VariableLayer
{
    [Serializable]
    public sealed class VariableRegistryDirectoryMetadata
    {
        public string DirectoryPath { get; }
        public string DisplayDirectoryPath { get; }

        public VariableRegistryDirectoryMetadata(string directoryPath, string? displayDirectoryPath = null)
        {
            DirectoryPath = directoryPath ?? string.Empty;
            DisplayDirectoryPath = string.IsNullOrWhiteSpace(displayDirectoryPath)
                ? DirectoryPath
                : displayDirectoryPath!;
        }
    }

    [Serializable]
    public class VariableRegistryNode
    {
        public int Id { get; }
        public string StablePath { get; }
        public string DisplayPath { get; }
        public ValueKind ValueType { get; }
        public VariableRegistryDirectoryMetadata DirectoryMetadata { get; }

        public VariableRegistryNode(
            int id,
            string stablePath,
            string displayPath,
            ValueKind valueType,
            VariableRegistryDirectoryMetadata? directoryMetadata = null)
        {
            Id = id;
            StablePath = stablePath ?? string.Empty;
            DisplayPath = string.IsNullOrWhiteSpace(displayPath) ? StablePath : displayPath!;
            ValueType = valueType;
            DirectoryMetadata = directoryMetadata ?? new VariableRegistryDirectoryMetadata(string.Empty);
        }
    }

    public interface IVariablePropertyRegistry
    {
        IReadOnlyList<VariableRegistryNode> Nodes { get; }
        bool TryGetNode(int id, out VariableRegistryNode node);
        bool TryGetNode(string stablePath, out VariableRegistryNode node);
        bool TryGetValueType(int id, out ValueKind valueType);
    }

    public sealed class VariableRegistryCatalog : IVariablePropertyRegistry
    {
        readonly List<VariableRegistryNode> _nodes;
        readonly Dictionary<int, VariableRegistryNode> _nodesById;
        readonly Dictionary<string, VariableRegistryNode> _nodesByStablePath;

        public IReadOnlyList<VariableRegistryNode> Nodes => _nodes;

        public VariableRegistryCatalog(IEnumerable<VariableRegistryNode> nodes)
        {
            _nodes = new List<VariableRegistryNode>();
            _nodesById = new Dictionary<int, VariableRegistryNode>();
            _nodesByStablePath = new Dictionary<string, VariableRegistryNode>(StringComparer.Ordinal);

            if (nodes == null)
                return;

            foreach (var node in nodes)
            {
                if (node == null)
                    continue;
                if (_nodesById.ContainsKey(node.Id))
                    continue;
                if (_nodesByStablePath.ContainsKey(node.StablePath))
                    continue;

                _nodes.Add(node);
                _nodesById.Add(node.Id, node);
                _nodesByStablePath.Add(node.StablePath, node);
            }
        }

        public bool TryGetNode(int id, out VariableRegistryNode node)
        {
            return _nodesById.TryGetValue(id, out node!);
        }

        public bool TryGetNode(string stablePath, out VariableRegistryNode node)
        {
            if (string.IsNullOrWhiteSpace(stablePath))
            {
                node = null!;
                return false;
            }

            return _nodesByStablePath.TryGetValue(stablePath, out node!);
        }

        public bool TryGetValueType(int id, out ValueKind valueType)
        {
            if (_nodesById.TryGetValue(id, out var node))
            {
                valueType = node.ValueType;
                return true;
            }

            valueType = ValueKind.Null;
            return false;
        }
    }

    public sealed class VariableRegistryCatalogBuilder
    {
        readonly List<VariableRegistryNode> _nodes = new();

        public VariableRegistryCatalogBuilder Add(VariableRegistryNode node)
        {
            if (node != null)
                _nodes.Add(node);
            return this;
        }

        public VariableRegistryCatalog Build()
        {
            return new VariableRegistryCatalog(_nodes);
        }
    }
}
