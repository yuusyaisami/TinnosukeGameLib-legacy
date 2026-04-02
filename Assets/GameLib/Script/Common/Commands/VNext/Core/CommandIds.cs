#nullable enable

namespace Game.Commands.VNext
{
    public static class CommandIds
    {
        public const int Wait = 1001;
        public const int If = 1002;
        public const int Switch = 1003;
        public const int For = 1004;
        public const int Sequence = 1005;

        public const int ActionBlock = 1006;
        public const int Forget = 1007;
        public const int DelayExecutor = 1008;
        public const int AdvanceWait = 1009;
        public const int WaitEvent = 1010;

        public const int PlayAudio = 1100;
        public const int StopAudio = 1101;

        public const int SetVelocity = 1200;
        public const int AddForce = 1201;
        public const int SetChannelEnabled = 1202;
        public const int SetAllChannelsEnabled = 1203;
        public const int SetChannelInfluence = 1204;
        public const int ResetAllVelocities = 1205;
        public const int CreateMovementChannel = 1206;
        public const int RemoveMovementChannel = 1207;
        public const int SetMovementModule = 1208;
        public const int SetInputMovement = 1209;
        public const int MoveToPoints = 1210;
        public const int Teleport = 1211;

        public const int SelfDespawn = 1300;
        public const int SpawnParticle = 1400;
        public const int SpawnRuntimeTemplate = 1401;
        public const int RuntimeAllDelete = 1402;
        public const int SpawnRuntimeGrid = 1403;
        public const int StateMachine = 1500;
        public const int SetDirection = 1600;

        public const int ShowTooltip = 1700;
        public const int HideTooltip = 1701;
        public const int UIControl = 1702;
        public const int ShowToast = 1704;

        public const int RunFlow = 1800;
        public const int TextChannel = 1810;
        public const int AnimationSpriteChannel = 1820;
        public const int PublishEvent = 1830;
        public const int TransformAnimation = 1835;
        public const int VelocityDrivenRotation = 1836;
        public const int MeshChannelControl = 1837;
        public const int MeshMaterialFxControl = 1838;
        public const int ParallaxChannel = 1839;
        public const int UIDialogChannel = 1840;
        public const int WriteData = 1850;
        public const int WriteGridData = 1851;

        public const int BuildRoomMap = 1860;
        public const int ClearRoomMap = 1861;
        public const int RemoveRoomMapRect = 1862;
        public const int ApplyRoomMapVisual = 1863;
        public const int GetRoomMapCenter = 1864;
        public const int BuildMapNode = 1865;
        public const int MoveMapNode = 1866;
        public const int RunMapNodeCommands = 1867;
        public const int RefreshMapNodeState = 1868;
        public const int WriteMapNodePlayerState = 1869;

        public const int VisualSetState = 1870;
        public const int VisualBroadcast = 1871;

        public const int BuildUITraitList = 1872;
        public const int RefreshUITraitList = 1873;
        public const int SetUITraitListRange = 1874;
        public const int ClearUITraitList = 1875;
        public const int AddTraitToHolder = 1876;
        public const int RemoveTraitFromHolder = 1877;
        public const int UseTraitFromHolder = 1878;
        public const int ClearTraitFromHolder = 1879;

        public const int StageToggleWorld = 1880;
        public const int StagePeekOppositeWorld = 1881;
        public const int StageResetCurrentWorld = 1882;
        public const int StageGoal = 1883;
        public const int StageStartBuild = 1884;

        public const int RunPlayerCommands = 1890;
        public const int ChangeGameState = 1891;
        public const int SetFootTransformOffsetZ = 1900;
        public const int DebugCommandContext = 1930;
        public const int SetLifetimeScopeState = 1940;
        public const int MonitorChannelRuleControl = 1941;
        public const int ScopeLifecycleCondition = 1942;

        public const int SetCollisionEnabled = 1950;
        public const int CameraPostProcess = 1951;
        public const int CameraShake = 1952;
        public const int CameraZoom = 1953;
        public const int StateAnimationSetProfile = 1954;
        public const int SetTimeScale = 1955;
        public const int HealthApplyDamage = 1956;
        public const int HealthApplyHeal = 1957;
        public const int HealthControl = 1958;
        public const int SetUnityCollider = 1959;
        public const int SceneChange = 1960;
        public const int TimerControl = 1961;
        public const int HitColliderRuleControl = 1962;
        public const int WithHitColliderTargets = 1963;
        public const int TransformControllerRigidbody2D = 1964;
        public const int UnityRoomSendScore = 1965;
        public const int SetColliderSharedMaterial = 1966;
        public const int SetColliderPhysicsMaterialValues = 1967;
        public const int SetGlobalPhysics2D = 1968;
        public const int WorldPointerTargetControl = 1969;
        public const int Save = 1970;
        public const int SaveProfile = 1971;
        public const int LoadProfile = 1972;
        public const int ClearProfile = 1973;
        public const int ProfileChange = 1974;
        public const int DeleteAllSaveData = 1975;
        public const int UserMoveRotateRuntimeControl = 1976;
        public const int TargetChannelControl = 1977;
        public const int ButtonChannelHubControl = 1978;
        public const int ButtonChannelPlayerControl = 1979;
        public const int Light2DChannelHubControl = 1980;
        public const int Light2DChannelPlayerControl = 1981;
        public const int SliderControl = 1982;
        public const int TooltipChannelHubControl = 1983;
        public const int TooltipChannel = 1984;
        public const int TooltipChannelPlayerControl = 1985;
        public const int BindTraitListChannel = 1986;
        public const int RefreshTraitListChannel = 1987;
        public const int SetTraitListChannelRange = 1988;
        public const int ClearTraitListChannel = 1989;

        public const int WithActor = 2001;
        public const int WithActorDescendantRouter = 2002;
        public const int WithPlayer = 2003;
        public const int CommandChannelExecute = 2004;
        public const int SetContextSlot = 2005;
        public const int CommandChannelControl = 2006;
        public const int CommandListChannelHubControl = 2007;
        public const int CommandListChannelPlayerControl = 2008;
        public const int CommandListChannel = 2009;
        public const int HostCall = 3001;

        public const int EquipTrait = 2200;
        public const int WriteTraitData = 2201;
        public const int StatusEffectControl = 2202;
        public const int SharedLTSChannel = 2203;
        public const int Function = 2204;
        public const int TraitLottery = 2205;
        public const int PlaceTraitRuntime = 2206;
        public const int WriteStatusEffectData = 2207;
        public const int Lottery = 2208;
        public const int RuntimeTraitPresentationCommandMutation = 2209;

        public const int BackgroundLayer = 2300;

        // ゲームロジックのコマンド
        // Assets\Game\Script\Project\System\Stages\Core\StageManagerExecutors.cs
        // Assets\Game\Script\Project\System\Player\RunPlayerCommandsExecutor.cs
    }
}
