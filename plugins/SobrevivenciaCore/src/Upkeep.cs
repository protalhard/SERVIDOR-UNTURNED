using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>Upkeep de base por inatividade. Base = construções do mesmo grupo (ou do mesmo dono, se sem grupo).
    /// Ninguém da base entra há UpkeepAvisoDias: aviso (Discord DM pelo bot, via upkeep_log.txt).
    /// UpkeepSemProtecaoDias: perde a proteção offline. UpkeepApagarDias (0 = nunca): o servidor apaga a base.
    /// Não mexe em NPC, mostruário, base na safe, nem em peça do dono do servidor.</summary>
    public partial class Plugin
    {
        Dictionary<string, long> vistoUpkeep;          // "p<steam>" / "g<grupo>" -> unix da última vez online
        HashSet<string> avisadosUpkeep = new HashSet<string>();
        float proximoVistoUpkeep, proximaVarreduraUpkeep = -1f;
        string ArqVistoUpkeep => Path.Combine(Directory, "upkeep_visto.tsv");
        string ArqUpkeepLog => Path.Combine(Directory, "upkeep_log.txt");

        Dictionary<string, long> VistoUpkeep()
        {
            if (vistoUpkeep != null) return vistoUpkeep;
            vistoUpkeep = new Dictionary<string, long>();
            try
            {
                if (File.Exists(ArqVistoUpkeep))
                    foreach (var l in File.ReadAllLines(ArqVistoUpkeep))
                    {
                        var c = l.Split('\t');
                        if (c.Length >= 2 && long.TryParse(c[1], out var t)) vistoUpkeep[c[0]] = t;
                        if (c.Length >= 3 && c[2] == "avisado") avisadosUpkeep.Add(c[0]);
                    }
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            return vistoUpkeep;
        }

        void SalvarVistoUpkeep()
        {
            try { File.WriteAllLines(ArqVistoUpkeep, vistoUpkeep.Select(k => k.Key + "\t" + k.Value + (avisadosUpkeep.Contains(k.Key) ? "\tavisado" : ""))); }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
        }

        static string ChaveBase(ulong dono, ulong grupo) => grupo != 0 ? "g" + grupo : "p" + dono;

        /// <summary>Dias sem ninguém da base online (dono ou grupo). -1 = ainda não sabemos.</summary>
        double DiasSemNinguem(ulong dono, ulong grupo)
        {
            var v = VistoUpkeep();
            long ult = -1;
            if (dono != 0 && v.TryGetValue("p" + dono, out var a)) ult = Math.Max(ult, a);
            if (grupo != 0 && v.TryGetValue("g" + grupo, out var b)) ult = Math.Max(ult, b);
            return ult < 0 ? -1 : (Estatisticas.Agora - ult) / 86400.0;
        }

        /// <summary>Base abandonada perde a proteção offline (usado no Raid.cs).</summary>
        bool BaseAbandonada(ulong dono, ulong grupo) => S.UpkeepLigado && S.UpkeepSemProtecaoDias > 0 && DiasSemNinguem(dono, grupo) >= S.UpkeepSemProtecaoDias;

        void TickUpkeep()
        {
            if (!S.UpkeepLigado || !Level.isLoaded) return;
            float agora = Time.realtimeSinceStartup;
            var v = VistoUpkeep();
            if (agora >= proximoVistoUpkeep)
            {
                proximoVistoUpkeep = agora + 60f;
                long t = Estatisticas.Agora;
                foreach (var sp in Provider.clients)
                {
                    if (sp?.player == null) continue;
                    string kp = "p" + sp.playerID.steamID.m_SteamID;
                    v[kp] = t; avisadosUpkeep.Remove(kp);
                    var g = sp.player.quests.groupID.m_SteamID;
                    if (g != 0) { v["g" + g] = t; avisadosUpkeep.Remove("g" + g); }
                    var f = FaccaoDe(sp.playerID.steamID.m_SteamID);
                    if (f != null) { v["g" + f.Id] = t; avisadosUpkeep.Remove("g" + f.Id); }
                }
                SalvarVistoUpkeep();
            }
            if (proximaVarreduraUpkeep < 0) { proximaVarreduraUpkeep = agora + 300f; return; }
            if (agora < proximaVarreduraUpkeep) return;
            proximaVarreduraUpkeep = agora + 1800f;
            VarrerUpkeep();
        }

        class BaseUpkeep
        {
            public ulong Dono, Grupo; public int Pecas; public Vector3 Pos;
            public List<(BarricadeDrop d, byte x, byte y)> Barricadas = new List<(BarricadeDrop, byte, byte)>();
            public List<(StructureDrop d, byte x, byte y)> Estruturas = new List<(StructureDrop, byte, byte)>();
        }

        void VarrerUpkeep()
        {
            var v = VistoUpkeep();
            var bases = new Dictionary<string, BaseUpkeep>();
            BaseUpkeep Base(ulong dono, ulong grupo, Vector3 pos)
            {
                var k = ChaveBase(dono, grupo);
                if (!bases.TryGetValue(k, out var b)) bases[k] = b = new BaseUpkeep { Dono = dono, Grupo = grupo, Pos = pos };
                b.Pecas++;
                return b;
            }
            bool Ignorar(ulong dono, Vector3 pos) => dono == 0 || EhDono(dono) || ZonaSeguraEm(pos) != null;
            for (byte x = 0; x < Regions.WORLD_SIZE; x++)
                for (byte y = 0; y < Regions.WORLD_SIZE; y++)
                {
                    var rb = BarricadeManager.regions[x, y];
                    if (rb != null)
                        foreach (var d in rb.drops)
                        {
                            var sd = d?.GetServersideData();
                            if (sd == null || d.model == null || d.asset == null || EhNpc(d.asset.id) || PecaMontada(d.model)) continue;
                            if (Ignorar(sd.owner, d.model.position)) continue;
                            Base(sd.owner, sd.group, d.model.position).Barricadas.Add((d, x, y));
                        }
                    var rs = StructureManager.regions[x, y];
                    if (rs != null)
                        foreach (var d in rs.drops)
                        {
                            var sd = d?.GetServersideData();
                            if (sd == null || d.model == null) continue;
                            if (Ignorar(sd.owner, d.model.position)) continue;
                            Base(sd.owner, sd.group, d.model.position).Estruturas.Add((d, x, y));
                        }
                }
            long agora = Estatisticas.Agora;
            bool mudou = false;
            int apagadas = 0;
            foreach (var kv in bases)
            {
                var b = kv.Value;
                double dias = DiasSemNinguem(b.Dono, b.Grupo);
                if (dias < 0)
                {
                    // base de quem nunca vimos desde que o upkeep começou: conta a partir de agora
                    v[kv.Key] = agora; mudou = true;
                    continue;
                }
                string fac = FaccaoPorGrupo(b.Grupo) != null ? $" faccao {b.Grupo}" : "";
                string quem = $"{NomeJogador(b.Dono)} ({b.Dono})";
                if (S.UpkeepAvisoDias > 0 && dias >= S.UpkeepAvisoDias && !avisadosUpkeep.Contains(kv.Key))
                {
                    avisadosUpkeep.Add(kv.Key); mudou = true;
                    int faltam = S.UpkeepApagarDias > 0 ? Math.Max(0, (int)Math.Ceiling(S.UpkeepApagarDias - dias)) : -1;
                    LinhaUpkeep($"UPKEEP AVISO: base de {quem} com {b.Pecas} pecas, {(int)dias} dias sem ninguem, apaga em {faltam} dias, em {b.Pos.x:0} {b.Pos.z:0}{fac}");
                }
                if (S.UpkeepApagarDias > 0 && dias >= S.UpkeepApagarDias)
                {
                    foreach (var (d, x, y) in b.Barricadas)
                        try { BarricadeManager.destroyBarricade(d, x, y, ushort.MaxValue); } catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
                    foreach (var (d, x, y) in b.Estruturas)
                        try { StructureManager.destroyStructure(d, x, y, Vector3.zero); } catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
                    apagadas++;
                    LinhaUpkeep($"UPKEEP APAGOU: base de {quem} com {b.Pecas} pecas, {(int)dias} dias sem ninguem, em {b.Pos.x:0} {b.Pos.z:0}{fac}");
                }
            }
            if (mudou) SalvarVistoUpkeep();
            Rocket.Core.Logging.Logger.Log($"[Upkeep] {bases.Count} bases olhadas, {apagadas} apagadas");
        }

        void LinhaUpkeep(string texto)
        {
            Rocket.Core.Logging.Logger.Log("[Upkeep] " + texto);
            try { File.AppendAllText(ArqUpkeepLog, $"{AgoraBrasil:dd/MM HH:mm} {texto}{Environment.NewLine}"); } catch { }
        }
    }
}
