#nullable enable
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Trait
{
    /// <summary>
    /// Runtime inspector viewer for TraitHolderHub.
    /// </summary>
    [Serializable]
    public sealed class TraitHolderDebugViewer
    {
        [ShowInInspector, ReadOnly, LabelText("Holder Count")]
        public int HolderCount => _snapshotHolderCount;

        [ShowInInspector]
        [TableList(IsReadOnly = true, AlwaysExpanded = true)]
        public List<HolderRow> Holders => GetHolders();

        ITraitHolderHubService? _hub;
        readonly List<HolderRow> _holders = new();
        int _snapshotHolderCount;

        public void Bind(ITraitHolderHubService hub)
        {
            _hub = hub;
            Refresh();
        }

        [Button(ButtonSizes.Small)]
        public void Refresh()
        {
            BuildSnapshot();
        }

        List<HolderRow> GetHolders()
        {
            BuildSnapshot();
            return _holders;
        }

        void BuildSnapshot()
        {
            _holders.Clear();
            _snapshotHolderCount = 0;

            if (_hub == null)
                return;

            var keys = _hub.Keys;
            if (keys == null || keys.Count == 0)
                return;

            _snapshotHolderCount = keys.Count;
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i] ?? string.Empty;
                if (!_hub.TryGetHolder(key, out var holder) || holder == null)
                    continue;

                var row = new HolderRow
                {
                    Key = key,
                    HolderId = holder is TraitHolderService service ? service.HolderId : string.Empty,
                    Count = holder.Traits != null ? holder.Traits.Count : 0,
                };

                if (holder.Traits != null)
                {
                    for (int t = 0; t < holder.Traits.Count; t++)
                    {
                        var trait = holder.Traits[t];
                        if (trait == null)
                            continue;

                        var definition = trait.Definition;
                        string definitionName = definition is UnityEngine.Object obj ? obj.name : definition?.GetType().Name ?? string.Empty;
                        string definitionId = definition?.DefinitionId ?? string.Empty;
                        string instanceId = trait.InstanceId ?? string.Empty;

                        var traitRow = new TraitRow
                        {
                            DefinitionId = definitionId,
                            DefinitionName = definitionName,
                            InstanceId = instanceId,
                        };

                        if (holder is TraitHolderService richTextHolder)
                        {
                            if (richTextHolder.TryGetRichTextKeys(trait, out var descKey, out var nameKey))
                            {
                                traitRow.DescriptionKey = descKey;
                                traitRow.NameKey = nameKey;
                            }
                        }

                        row.Traits.Add(traitRow);
                    }
                }

                _holders.Add(row);
            }
        }

        [Serializable]
        public sealed class HolderRow
        {
            [TableColumnWidth(160)] public string Key = string.Empty;
            [TableColumnWidth(160)] public string HolderId = string.Empty;
            [TableColumnWidth(60)] public int Count;
            [TableList(IsReadOnly = true, AlwaysExpanded = true)]
            public List<TraitRow> Traits = new();
        }

        [Serializable]
        public sealed class TraitRow
        {
            [TableColumnWidth(160)] public string DefinitionId = string.Empty;
            [TableColumnWidth(160)] public string DefinitionName = string.Empty;
            [TableColumnWidth(200)] public string InstanceId = string.Empty;
            [TableColumnWidth(260)] public string DescriptionKey = string.Empty;
            [TableColumnWidth(260)] public string NameKey = string.Empty;
        }
    }
}
