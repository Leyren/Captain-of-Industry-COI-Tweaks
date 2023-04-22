using Mafi;
using Mafi.Base;
using Mafi.Collections;
using Mafi.Core;
using Mafi.Core.Mods;
using Mafi.Core.Prototypes;
using Mafi.Core.Game;
using COITweaks.Config;

namespace COITweaks
{

    public sealed class COITweaks : IMod {

        private Logger log = Logger.WithName("Main");
        public bool IsUiOnly => false;

        public string Name => "COITweaks";

        public int Version => 1;


        // Mod constructor that lists mod dependencies as parameters.
        // This guarantee that all listed mods will be loaded before this mod.
        // It is a good idea to depend on both `Mafi.Core.CoreMod` and `Mafi.Base.BaseMod`.
        public COITweaks(CoreMod coreMod, BaseMod baseMod) {
            // You can use Log class for logging. These will be written to the log file
            // and can be also displayed in the in-game console with command `also_log_to_console`.
            log.Info("Constructed");
	    }

        public void RegisterDependencies(DependencyResolverBuilder depBuilder, ProtosDb protosDb, bool gameWasLoaded)
        {
            log.Info("RegisterDependencies - start");
            if (!ConfigReader.Instance().GetBool(ConfigReader.ENABLE, true))
            {
                log.Info($"Mod is disabled - do nothing (Config: {ConfigReader.ENABLE}={ConfigReader.Instance().GetValue(ConfigReader.ENABLE)}");
                return;
            }

            EnableIf<PillarTweaksController>(depBuilder, ConfigReader.PILLAR_TWEAKS_ENABLE);
            EnableIf<ConfigTweaks>(depBuilder, ConfigReader.CONFIG_TWEAKS_ENABLE, false);

            //log.Info(depBuilder.PrintCurrentRegistrations());
            log.Info("RegisterDependencies - done");
        }

        private void EnableIf<C>(DependencyResolverBuilder depBuilder, string configProperty, bool withInterfaces=true) where C: class
        {
            if (ConfigReader.Instance().GetBool(configProperty, true))
            {
                log.Info($"Enable Component {typeof(C)} (Config: {configProperty}={ConfigReader.Instance().GetValue(configProperty)})");
                if (withInterfaces)
                {
                    depBuilder.RegisterDependency<C>().AsSelf().AsAllInterfaces();
                }
                else
                {
                    depBuilder.RegisterDependency<C>().AsSelf();
                }
            }
            else
            {
                log.Warn($"Disable Component {typeof(C)} (Config: {configProperty}={ConfigReader.Instance().GetValue(configProperty)})");
            }
        }

        public void ChangeConfigs(Lyst<IConfig> configs)
        {
            log.Info("Change Configs - do nothing");
        }

        public void Initialize(DependencyResolver resolver, bool gameWasLoaded)
        {
            log.Info("Initialize - do nothing");
        }

        public void RegisterPrototypes(ProtoRegistrator registrator)
        {
            log.Info("Registering prototypes - do nothing");
        }

    }
}