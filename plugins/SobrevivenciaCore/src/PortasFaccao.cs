using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SDG.NetTransport;
using SDG.Unturned;

namespace SobrevivenciaCore
{
    /// <summary>O jogo não tem evento "antes de abrir a porta", então a porta da facção é conferida com um patch (Harmony)
    /// no pedido do jogador. A dll do Harmony vai embutida no plugin: se o servidor já tiver uma, usa a dele.</summary>
    public static class PortasFaccao
    {
        static bool resolvendo;
        static object harmony;

        static Assembly Resolver(object s, ResolveEventArgs e)
        {
            if (!e.Name.StartsWith("0Harmony", StringComparison.OrdinalIgnoreCase)) return null;
            var ja = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "0Harmony");
            if (ja != null) return ja;
            using (var st = typeof(PortasFaccao).Assembly.GetManifestResourceStream("0Harmony.dll"))
            {
                if (st == null) return null;
                var b = new byte[st.Length];
                st.Read(b, 0, b.Length);
                return Assembly.Load(b);
            }
        }

        public static void Ligar()
        {
            if (!resolvendo) { AppDomain.CurrentDomain.AssemblyResolve += Resolver; resolvendo = true; }
            try { Aplicar(); }
            catch (Exception e)
            {
                Rocket.Core.Logging.Logger.LogWarning("SobrevivenciaCore: patch da porta da faccao nao carregou (portas ficam pelo grupo normal): " + e.Message);
            }
        }

        static void Aplicar()
        {
            var h = new Harmony("dz.sobrevivencia.portas");
            var alvo = typeof(InteractableDoor).GetMethod("ReceiveToggleRequest", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (alvo == null) throw new Exception("InteractableDoor.ReceiveToggleRequest nao existe nesta versao");
            h.Patch(alvo, prefix: new HarmonyMethod(typeof(PortasFaccao).GetMethod(nameof(Antes), BindingFlags.Static | BindingFlags.NonPublic)));
            harmony = h;
            Rocket.Core.Logging.Logger.Log("SobrevivenciaCore: portas da faccao conferem o cargo (patch ligado).");
        }

        public static void Desligar()
        {
            try { (harmony as Harmony)?.UnpatchAll("dz.sobrevivencia.portas"); } catch { }
            harmony = null;
        }

        static bool Antes(InteractableDoor __instance, in ServerInvocationContext context)
        {
            try
            {
                var pl = context.GetPlayer();
                var pg = Plugin.Instancia;
                return pg == null || pg.PodeUsarPorta(pl, __instance);
            }
            catch { return true; }
        }
    }
}
