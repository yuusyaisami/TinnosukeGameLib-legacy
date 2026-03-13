#nullable enable

namespace Game.MapNode
{
    public enum MapNodeType
    {
        None = 0,
        Default = 1,
        // ここからはユーザー定義タイプ
        NormalEnemy = 100,
        BossEnemy = 101,
        Divination = 102,
        Shop = 103,
    }

    public enum MapNodeState
    {
        None = 0,
        Locked = 1,
        Available = 2,
        Visited = 3,
        Completed = 4,
        Disabled = 5
    }

    public enum MapNodeSpace
    {
        World = 0,
        UI = 1
    }

    public enum MapNodeSpawnSource
    {
        Prefab = 0,
        RuntimeTemplate = 1
    }

    public enum MapNodeFailurePolicy
    {
        ContinueOnError = 0,
        FailFast = 1
    }
}
