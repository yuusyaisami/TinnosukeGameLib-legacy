#nullable enable
using System.Collections.Generic;
using Game.Common;
using Game.VariableLayer;
using UnityEngine;

namespace Game.Channel
{
    public interface IMeshMaterialPropertyRegistry : IVariablePropertyRegistry
    {
        bool TryGetMeshNode(int id, out MeshMaterialPropertyNode node);
    }

    public sealed class MeshMaterialPropertyNode : VariableRegistryNode
    {
        public string ShaderPropertyName { get; }
        public int ShaderPropertyId { get; }

        public MeshMaterialPropertyNode(
            int id,
            string stablePath,
            string displayPath,
            ValueKind valueType,
            string shaderPropertyName,
            VariableRegistryDirectoryMetadata? directoryMetadata = null)
            : base(id, stablePath, displayPath, valueType, directoryMetadata)
        {
            ShaderPropertyName = shaderPropertyName ?? string.Empty;
            ShaderPropertyId = string.IsNullOrWhiteSpace(shaderPropertyName)
                ? 0
                : Shader.PropertyToID(shaderPropertyName);
        }
    }

    public sealed class MeshMaterialPropertyCatalog : IMeshMaterialPropertyRegistry
    {
        public static class Ids
        {
            public const int BaseTint = 1000;

            public const int ContourGradientEnabled = 1100;
            public const int ContourGradientColor = 1101;
            public const int ContourGradientBlendMode = 1102;
            public const int ContourGradientStrength = 1103;
            public const int ContourGradientRange = 1104;
            public const int ContourGradientFalloff = 1105;

            public const int EdgeAlphaEnabled = 1200;
            public const int EdgeAlphaMode = 1201;
            public const int EdgeAlphaGain = 1202;
            public const int EdgeAlphaRange = 1203;
            public const int EdgeAlphaSoftness = 1204;

            public const int BandsEnabled = 1300;
            public const int BandsCount = 1301;
            public const int BandsContrast = 1302;
            public const int BandsColor = 1303;
            public const int BandsBlendMode = 1304;
            public const int BandsIntensity = 1305;

            public const int EdgeFlowEnabled = 1400;
            public const int EdgeFlowColor = 1401;
            public const int EdgeFlowBlendMode = 1402;
            public const int EdgeFlowWidth = 1403;
            public const int EdgeFlowSpeed = 1404;
            public const int EdgeFlowIntensity = 1405;

            public const int InteriorNoiseEnabled = 1500;
            public const int InteriorNoiseScale = 1501;
            public const int InteriorNoiseSpeed = 1502;
            public const int InteriorNoiseStrength = 1503;
        }

        public static MeshMaterialPropertyCatalog Instance { get; } = Build();

        readonly VariableRegistryCatalog _baseCatalog;
        readonly Dictionary<int, MeshMaterialPropertyNode> _nodesById;
        readonly List<VariableRegistryNode> _nodes;

        public IReadOnlyList<VariableRegistryNode> Nodes => _nodes;

        MeshMaterialPropertyCatalog(IEnumerable<MeshMaterialPropertyNode> nodes)
        {
            _nodesById = new Dictionary<int, MeshMaterialPropertyNode>();
            var baseNodes = new List<VariableRegistryNode>();

            foreach (var node in nodes)
            {
                if (node == null || _nodesById.ContainsKey(node.Id))
                    continue;

                _nodesById.Add(node.Id, node);
                baseNodes.Add(node);
            }

            _baseCatalog = new VariableRegistryCatalog(baseNodes);
            _nodes = new List<VariableRegistryNode>(baseNodes);
        }

        public bool TryGetMeshNode(int id, out MeshMaterialPropertyNode node)
        {
            return _nodesById.TryGetValue(id, out node!);
        }

        public bool TryGetNode(int id, out VariableRegistryNode node)
        {
            return _baseCatalog.TryGetNode(id, out node!);
        }

        public bool TryGetNode(string stablePath, out VariableRegistryNode node)
        {
            return _baseCatalog.TryGetNode(stablePath, out node!);
        }

        public bool TryGetValueType(int id, out ValueKind valueType)
        {
            return _baseCatalog.TryGetValueType(id, out valueType);
        }

        static MeshMaterialPropertyCatalog Build()
        {
            var nodes = new List<MeshMaterialPropertyNode>
            {
                Create(Ids.BaseTint, "Mesh/Base/Tint", "Base/Tint", ValueKind.Color, "_MeshBaseColor", "Base"),

                Create(Ids.ContourGradientEnabled, "Mesh/ContourGradient/Enabled", "Contour Gradient/Enabled", ValueKind.Bool, "_MeshContourGradientEnabled", "Contour Gradient"),
                Create(Ids.ContourGradientColor, "Mesh/ContourGradient/Color", "Contour Gradient/Color", ValueKind.Color, "_MeshContourGradientColor", "Contour Gradient"),
                Create(Ids.ContourGradientBlendMode, "Mesh/ContourGradient/BlendMode", "Contour Gradient/Blend Mode", ValueKind.Int, "_MeshContourGradientBlendMode", "Contour Gradient"),
                Create(Ids.ContourGradientStrength, "Mesh/ContourGradient/Strength", "Contour Gradient/Strength", ValueKind.Float, "_MeshContourGradientStrength", "Contour Gradient"),
                Create(Ids.ContourGradientRange, "Mesh/ContourGradient/Range", "Contour Gradient/Range", ValueKind.Float, "_MeshContourGradientRange", "Contour Gradient"),
                Create(Ids.ContourGradientFalloff, "Mesh/ContourGradient/Falloff", "Contour Gradient/Falloff", ValueKind.Float, "_MeshContourGradientFalloff", "Contour Gradient"),

                Create(Ids.EdgeAlphaEnabled, "Mesh/EdgeAlpha/Enabled", "Edge Alpha/Enabled", ValueKind.Bool, "_MeshEdgeAlphaEnabled", "Edge Alpha"),
                Create(Ids.EdgeAlphaMode, "Mesh/EdgeAlpha/Mode", "Edge Alpha/Mode", ValueKind.Int, "_MeshEdgeAlphaMode", "Edge Alpha"),
                Create(Ids.EdgeAlphaGain, "Mesh/EdgeAlpha/Gain", "Edge Alpha/Gain", ValueKind.Float, "_MeshEdgeAlphaGain", "Edge Alpha"),
                Create(Ids.EdgeAlphaRange, "Mesh/EdgeAlpha/Range", "Edge Alpha/Range", ValueKind.Float, "_MeshEdgeAlphaRange", "Edge Alpha"),
                Create(Ids.EdgeAlphaSoftness, "Mesh/EdgeAlpha/Softness", "Edge Alpha/Softness", ValueKind.Float, "_MeshEdgeAlphaSoftness", "Edge Alpha"),

                Create(Ids.BandsEnabled, "Mesh/Bands/Enabled", "Bands/Enabled", ValueKind.Bool, "_MeshBandsEnabled", "Bands"),
                Create(Ids.BandsCount, "Mesh/Bands/Count", "Bands/Count", ValueKind.Int, "_MeshBandsCount", "Bands"),
                Create(Ids.BandsContrast, "Mesh/Bands/Contrast", "Bands/Contrast", ValueKind.Float, "_MeshBandsContrast", "Bands"),
                Create(Ids.BandsColor, "Mesh/Bands/Color", "Bands/Color", ValueKind.Color, "_MeshBandsColor", "Bands"),
                Create(Ids.BandsBlendMode, "Mesh/Bands/BlendMode", "Bands/Blend Mode", ValueKind.Int, "_MeshBandsBlendMode", "Bands"),
                Create(Ids.BandsIntensity, "Mesh/Bands/Intensity", "Bands/Intensity", ValueKind.Float, "_MeshBandsIntensity", "Bands"),

                Create(Ids.EdgeFlowEnabled, "Mesh/EdgeFlow/Enabled", "Edge Flow/Enabled", ValueKind.Bool, "_MeshEdgeFlowEnabled", "Edge Flow"),
                Create(Ids.EdgeFlowColor, "Mesh/EdgeFlow/Color", "Edge Flow/Color", ValueKind.Color, "_MeshEdgeFlowColor", "Edge Flow"),
                Create(Ids.EdgeFlowBlendMode, "Mesh/EdgeFlow/BlendMode", "Edge Flow/Blend Mode", ValueKind.Int, "_MeshEdgeFlowBlendMode", "Edge Flow"),
                Create(Ids.EdgeFlowWidth, "Mesh/EdgeFlow/Width", "Edge Flow/Width", ValueKind.Float, "_MeshEdgeFlowWidth", "Edge Flow"),
                Create(Ids.EdgeFlowSpeed, "Mesh/EdgeFlow/Speed", "Edge Flow/Speed", ValueKind.Float, "_MeshEdgeFlowSpeed", "Edge Flow"),
                Create(Ids.EdgeFlowIntensity, "Mesh/EdgeFlow/Intensity", "Edge Flow/Intensity", ValueKind.Float, "_MeshEdgeFlowIntensity", "Edge Flow"),

                Create(Ids.InteriorNoiseEnabled, "Mesh/InteriorNoise/Enabled", "Interior Noise/Enabled", ValueKind.Bool, "_MeshInteriorNoiseEnabled", "Interior Noise"),
                Create(Ids.InteriorNoiseScale, "Mesh/InteriorNoise/Scale", "Interior Noise/Scale", ValueKind.Float, "_MeshInteriorNoiseScale", "Interior Noise"),
                Create(Ids.InteriorNoiseSpeed, "Mesh/InteriorNoise/Speed", "Interior Noise/Speed", ValueKind.Float, "_MeshInteriorNoiseSpeed", "Interior Noise"),
                Create(Ids.InteriorNoiseStrength, "Mesh/InteriorNoise/Strength", "Interior Noise/Strength", ValueKind.Float, "_MeshInteriorNoiseStrength", "Interior Noise"),
            };

            return new MeshMaterialPropertyCatalog(nodes);
        }

        static MeshMaterialPropertyNode Create(
            int id,
            string stablePath,
            string displayPath,
            ValueKind valueType,
            string shaderPropertyName,
            string directoryPath)
        {
            return new MeshMaterialPropertyNode(
                id,
                stablePath,
                displayPath,
                valueType,
                shaderPropertyName,
                new VariableRegistryDirectoryMetadata(directoryPath));
        }
    }
}
