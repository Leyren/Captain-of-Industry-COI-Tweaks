using Mafi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace COITweaks
{
    internal class Logger
    {
        private static string prefix = "[COI Tweaks Mod]";
        private string namePrefix;

        public delegate string ErrorSupplier();

        public Logger(string name)
        {
            this.namePrefix = $"[{name}]";
        }

        public static Logger WithName(string name)
        {
            return new Logger(name);    
        }

        public void Info(string message)
        {
            Log.Info($"{prefix} {namePrefix} {message}");
        }

        public void Warn(string warning)
        {
            Log.Warning($"{prefix} {namePrefix}{warning}");
        }
        public void Error(string error)
        {
            Log.Error($"{prefix} {namePrefix}{error}");
        }

        public void ErrorIf(bool condition, string message)
        {
            if (!condition) return;
            Error(message);
        }

        public void ErrorIf(bool condition, ErrorSupplier supplier)
        {
            if (!condition) return;
            Error(supplier.Invoke());
        }
    }
}
