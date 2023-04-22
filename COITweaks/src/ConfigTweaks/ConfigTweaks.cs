using Mafi.Core.Entities.Static;
using Mafi.Core.Factory.Transports;
using Mafi.Core.Prototypes;
using Mafi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Mafi.Base;
using Mafi.Core.Game;
using System.CodeDom;

namespace COITweaks.Config
{
    public class ConfigTweaks
    {
        // field names to use in reflection
        private static readonly string MAX_PILLAR_SUPPORT_RADIUS = "MaxPillarSupportRadius";
        private static readonly string MAX_PILLAR_HEIGHT = "MAX_PILLAR_HEIGHT";

        private Logger log = Logger.WithName("Config Tweaks");
        private readonly ProtosDb protosDb;

        public ConfigTweaks(ProtosDb protosDb) { 
            this.protosDb = protosDb;
            Apply();
        }

        private static readonly StaticEntityProto.ID[] PIPES = { Ids.Transports.PipeT1, Ids.Transports.PipeT2, Ids.Transports.PipeT3  };
        private static readonly StaticEntityProto.ID[] CONVEYORS = { Ids.Transports.FlatConveyor, Ids.Transports.FlatConveyorT2, Ids.Transports.FlatConveyorT3 };
        private static readonly StaticEntityProto.ID[] LOOSE_CONVEYORS = { Ids.Transports.LooseMaterialConveyor, Ids.Transports.LooseMaterialConveyorT2, Ids.Transports.LooseMaterialConveyorT3 };
        private static readonly StaticEntityProto.ID[] MOLTEN_CHANNEL = { Ids.Transports.MoltenMetalChannel };
        private static readonly StaticEntityProto.ID[] SHAFT = { Ids.Transports.Shaft };

        public void Apply()
        {
            log.Info("Apply tweaks");
            ConfigReader.Instance().ProcessInt(ConfigReader.FLAT_CONVEYOR_SUPPORT_RADIUS,
                (value, key) => UpdateField<TransportProto, RelTile1i>(MAX_PILLAR_SUPPORT_RADIUS, new RelTile1i(value), rt => rt.ToString(), key, CONVEYORS));
            ConfigReader.Instance().ProcessInt(ConfigReader.PIPE_SUPPORT_RADIUS,
                (value, key) => UpdateField<TransportProto, RelTile1i>(MAX_PILLAR_SUPPORT_RADIUS, new RelTile1i(value), rt => rt.ToString(), key, PIPES));
            ConfigReader.Instance().ProcessInt(ConfigReader.LOOSE_MATERIAL_CONVEYOR_SUPPORT_RADIUS,
                (value, key) => UpdateField<TransportProto, RelTile1i>(MAX_PILLAR_SUPPORT_RADIUS, new RelTile1i(value), rt => rt.ToString(), key, LOOSE_CONVEYORS));
            ConfigReader.Instance().ProcessInt(ConfigReader.MOLTEN_METAL_CHANNEL_SUPPORT_RADIUS,
                (value, key) => UpdateField<TransportProto, RelTile1i>(MAX_PILLAR_SUPPORT_RADIUS, new RelTile1i(value), rt => rt.ToString(), key, MOLTEN_CHANNEL));
            ConfigReader.Instance().ProcessInt(ConfigReader.SHAFT_SUPPORT_RADIUS,
                (value, key) => UpdateField<TransportProto, RelTile1i>(MAX_PILLAR_SUPPORT_RADIUS, new RelTile1i(value), rt => rt.ToString(), key, SHAFT));

            ConfigReader.Instance().ProcessInt(ConfigReader.MAX_PILLAR_HEIGHT, (value, key) =>
            UpdateStaticField<TransportPillarProto, ThicknessTilesI>(MAX_PILLAR_HEIGHT, new ThicknessTilesI(value), val => val.Value.ToString(), key));
        }

        /// <summary>
        /// Updates the field with the provided new value via reflection.
        /// </summary>
        private void UpdateField<PROTO, FIELD>(string fieldName, FIELD newValue, Func<FIELD, string> toStringFunc, string configProperty, params StaticEntityProto.ID[] ids) where PROTO : Proto
        {
            foreach (var id in ids)
            {
                PROTO proto = protosDb.Get(id).ValueOrThrow("proto not found") as PROTO;
                FieldInfo field = typeof(TransportProto).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                var previous = toStringFunc((FIELD)field.GetValue(proto));
                field.SetValue(proto, newValue);
                log.Info($"Change {fieldName} of {id} from {previous} to {toStringFunc(newValue)} (from: config {configProperty})");
            }
        }

        private void UpdateStaticField<PROTO, FIELD>(string property, FIELD newValue, Func<FIELD, string> toStringFunc, string configProperty) where PROTO : Proto
        {
            FieldInfo field = typeof(PROTO).GetField(property, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
            var previous = toStringFunc((FIELD)field.GetValue(null));
            field.SetValue(null, newValue);
            log.Info($"Change {property} of {typeof(PROTO)} from {previous} to {toStringFunc(newValue)} (from: config {configProperty})");
        }
    }
}
