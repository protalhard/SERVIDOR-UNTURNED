using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace SobrevivenciaCore
{
    /// <summary>/montar safe|negro|cacador 1-3: monta o modelo escolhido em volta do admin (ele fica no centro,
    /// olhando pra onde quer a entrada). Os modelos vêm de montar_modelos.tsv (gerado por modelos/exportar.py;
    /// um arquivo com esse nome na pasta do plugin substitui o embutido). /desmontar desfaz a última.</summary>
    public partial class Plugin
    {
        string ArqMontagens => Path.Combine(Directory, "montagens.tsv");
        HashSet<uint> pecasMontadas;

        class PecaModelo
        {
            public string Tipo, Extra;
            public float X, Y, X2, Y2;
        }

        Dictionary<string, List<PecaModelo>> modelosMontar;

        Dictionary<string, List<PecaModelo>> ModelosMontar()
        {
            if (modelosMontar != null) return modelosMontar;
            string texto = null;
            var arq = Path.Combine(Directory, "montar_modelos.tsv");
            try
            {
                if (File.Exists(arq)) texto = File.ReadAllText(arq);
                else
                    using (var st = typeof(Plugin).Assembly.GetManifestResourceStream("montar_modelos.tsv"))
                    using (var r = new StreamReader(st)) texto = r.ReadToEnd();
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            var res = new Dictionary<string, List<PecaModelo>>();
            foreach (var l in (texto ?? "").Split('\n'))
            {
                if (l.StartsWith("#") || l.Trim() == "") continue;
                var c = l.TrimEnd('\r').Split('\t');
                if (c.Length < 3) continue;
                float F(int i) => c.Length > i && float.TryParse(c[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
                var pc = new PecaModelo { Tipo = c[1], X = F(2), Y = F(3), X2 = F(4), Y2 = F(5), Extra = c.Length > 6 ? c[6] : "" };
                if (!res.TryGetValue(c[0], out var lista)) res[c[0]] = lista = new List<PecaModelo>();
                lista.Add(pc);
            }
            return modelosMontar = res;
        }

        /// <summary>Peças do /montar não quebram e não abrem (enfeite).</summary>
        public bool PecaMontada(Transform t)
        {
            if (pecasMontadas == null)
            {
                pecasMontadas = new HashSet<uint>();
                try
                {
                    if (File.Exists(ArqMontagens))
                        foreach (var l in File.ReadAllLines(ArqMontagens))
                        {
                            var c = l.Split('\t');
                            if (c.Length > 2) foreach (var x in c[2].Split(',')) if (uint.TryParse(x, out var v)) pecasMontadas.Add(v);
                        }
                }
                catch { }
            }
            if (pecasMontadas.Count == 0 || t == null) return false;
            var d = BarricadeManager.FindBarricadeByRootTransform(t);
            return d != null && pecasMontadas.Contains(d.instanceID);
        }

        ushort IdPeca(string peca, string area)
        {
            string Achar(string chave)
            {
                foreach (var s in S.MontarPecas ?? new List<string>())
                {
                    var i = s.IndexOf('=');
                    if (i > 0 && s.Substring(0, i).Trim().Equals(chave, StringComparison.OrdinalIgnoreCase)) return s.Substring(i + 1).Trim();
                }
                return null;
            }
            var v = Achar(peca + "@" + area) ?? Achar(peca);
            // "51266|6173": usa a primeira que existe no servidor (peça do pack DZ ou uma do jogo no lugar)
            foreach (var x in (v ?? "").Split('|'))
                if (ushort.TryParse(x.Trim(), out var id) && Assets.find(EAssetType.ITEM, id) is ItemBarricadeAsset) return id;
            return 0;
        }

        static float Chao(Vector3 pos)
        {
            // chão de verdade (terreno, estrada, prédio), ignorando barricadas já colocadas
            int mascara = RayMasks.GROUND | RayMasks.GROUND2 | RayMasks.ENVIRONMENT | RayMasks.LARGE | RayMasks.MEDIUM;
            var de = new Vector3(pos.x, pos.y + 25f, pos.z);
            if (Physics.Raycast(de, Vector3.down, out var hit, 60f, mascara)) return hit.point.y;
            return LevelGround.getHeight(pos);
        }

        /// <summary>Chão sob a peça: o ponto mais alto entre o centro e 4 pontos em volta (assim ela não afunda no lado mais alto).
        /// Ignora pontos muito acima do centro (telhado, pedra), pra peça não subir em cima deles.</summary>
        static float ChaoPeca(Vector3 pos, float raio = .5f)
        {
            float c = Chao(pos), h = c;
            foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                float y = Chao(pos + d * raio);
                if (y > h && y < c + 1.2f) h = y;
            }
            return h;
        }

        /// <summary>Largura de verdade de uma cerca/parede: coloca uma de teste, mede os colisores e tira. 0 = não deu pra medir.</summary>
        float MedirLargura(ushort id, Vector3 onde, float yaw, ulong dono)
        {
            if (larguraPecas.TryGetValue(id, out var w)) return w;
            w = 0f;
            BarricadeDrop d = null;
            try
            {
                d = Colocar(id, onde, yaw, dono);
                if (d?.model != null)
                {
                    var eixo = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                    float min = float.MaxValue, max = float.MinValue;
                    foreach (var col in d.model.GetComponentsInChildren<Collider>(true))
                    {
                        if (col.isTrigger || col.gameObject.layer != LayerMasks.BARRICADE) continue;
                        float c, meia;
                        if (col is BoxCollider bc)
                        {
                            var t = bc.transform;
                            c = Vector3.Dot(t.TransformPoint(bc.center), eixo);
                            meia = Mathf.Abs(Vector3.Dot(t.TransformVector(new Vector3(bc.size.x / 2, 0, 0)), eixo))
                                 + Mathf.Abs(Vector3.Dot(t.TransformVector(new Vector3(0, bc.size.y / 2, 0)), eixo))
                                 + Mathf.Abs(Vector3.Dot(t.TransformVector(new Vector3(0, 0, bc.size.z / 2)), eixo));
                        }
                        else
                        {
                            var b = col.bounds;
                            c = Vector3.Dot(b.center, eixo);
                            meia = Mathf.Abs(eixo.x) * b.extents.x + Mathf.Abs(eixo.z) * b.extents.z;
                        }
                        min = Mathf.Min(min, c - meia); max = Mathf.Max(max, c + meia);
                    }
                    if (max > min) w = max - min;
                }
            }
            catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            finally
            {
                if (d?.model != null && BarricadeManager.tryGetRegion(d.model, out var x, out var y, out var plant, out _))
                    BarricadeManager.destroyBarricade(d, x, y, plant);
            }
            if (w < .4f || w > 12f) w = 0f;
            if (w > 0f) larguraPecas[id] = w;
            return w;
        }
        readonly Dictionary<ushort, float> larguraPecas = new Dictionary<ushort, float>();

        BarricadeDrop Colocar(ushort id, Vector3 pos, float yaw, ulong dono, bool npc = false, float inclinar = 0f)
        {
            if (id == 0 || !(Assets.find(EAssetType.ITEM, id) is ItemBarricadeAsset asset)) return null;
            // padrão do Unturned: Euler(-90, yaw, 0) fica em pé virado pro yaw; a origem fica "offset" acima do chão
            var rot = Quaternion.Euler(npc ? S.MontarRotacaoXNpc : S.MontarRotacaoX, yaw, 0f);
            // cerca/parede em ladeira: inclina ao longo da linha pra acompanhar o chão (graus, + = lado direito sobe)
            if (Mathf.Abs(inclinar) > .5f) rot = Quaternion.AngleAxis(inclinar, Quaternion.Euler(0f, yaw, 0f) * Vector3.forward) * rot;
            pos.y += asset.offset;
            var t = BarricadeManager.dropNonPlantedBarricade(new Barricade(asset), pos, rot, dono, 0);
            return t != null ? BarricadeManager.FindBarricadeByRootTransform(t) : null;
        }

        /// <summary>Itens pra expor: "loja" ou "loja:Categoria,Categoria".</summary>
        List<ushort> ItensExpostos(string spec)
        {
            if (string.IsNullOrEmpty(spec)) return new List<ushort>();
            var partes = spec.Split(':');
            var loja = Configuration.Instance.Lojas.FirstOrDefault(l => l.Id == partes[0]);
            if (loja == null) return new List<ushort>();
            var cats = partes.Length > 1 ? partes[1].Split(',') : null;
            return loja.Itens.Where(i => !i.Veiculo && (cats == null || cats.Contains(i.Categoria)) && Itens.Existe(i.Id))
                .Select(i => i.Id).Distinct().ToList();
        }

        /// <summary>Procura em volta do admin o lugar mais plano e livre pro modelo, leva ele até lá e marca no mapa.</summary>
        public string ProcurarLugar(UnturnedPlayer adm, string area, int modelo, float raioBusca = 1500f)
        {
            if (!ModelosMontar().TryGetValue(area + modelo, out var pecas)) return "Modelo nao existe. Use /montar safe|negro|cacador 1|2|3";
            float r = 0f;
            foreach (var pc in pecas)
            {
                if (pc.Tipo == "zona") continue;
                r = Mathf.Max(r, Mathf.Sqrt(pc.X * pc.X + pc.Y * pc.Y));
                if (pc.Tipo == "cerca" || pc.Tipo == "parede") r = Mathf.Max(r, Mathf.Sqrt(pc.X2 * pc.X2 + pc.Y2 * pc.Y2));
            }
            r += 2f;
            // pontos do chão do modelo (círculo de raio r): grosso pra varrer, fino pra conferir
            List<Vector2> Pontos(int aneis, int porAnel)
            {
                var l = new List<Vector2> { Vector2.zero };
                for (int a = 1; a <= aneis; a++)
                    for (int k = 0; k < porAnel * a; k++)
                    {
                        float ang = k * Mathf.PI * 2f / (porAnel * a);
                        l.Add(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (r * a / aneis));
                    }
                return l;
            }
            var grosso = Pontos(2, 6);  // 19 pontos
            var fino = Pontos(4, 8);    // 81 pontos
            float metade = Level.size / 2f - Level.border - r - 5f;
            var origem = adm.Position;
            float passo = 25f;
            var candidatos = new List<KeyValuePair<float, Vector2>>();
            for (float dx = -raioBusca; dx <= raioBusca; dx += passo)
                for (float dz = -raioBusca; dz <= raioBusca; dz += passo)
                {
                    if (dx * dx + dz * dz > raioBusca * raioBusca) continue;
                    float cx = origem.x + dx, cz = origem.z + dz;
                    if (Mathf.Abs(cx) > metade || Mathf.Abs(cz) > metade) continue;
                    float mn = float.MaxValue, mx = float.MinValue;
                    foreach (var q in grosso)
                    {
                        float h = LevelGround.getHeight(new Vector3(cx + q.x, 0f, cz + q.y));
                        if (h < mn) mn = h; if (h > mx) mx = h;
                    }
                    candidatos.Add(new KeyValuePair<float, Vector2>(mx - mn, new Vector2(cx, cz)));
                }
            if (candidatos.Count == 0) return "Nao achei espaco dentro do mapa aqui. Va mais pro meio do mapa e tente de novo.";
            // peças já colocadas (bases de jogador e áreas já montadas): o lugar tem que estar livre delas
            var ocupados = new List<Vector2>();
            foreach (var reg in BarricadeManager.regions)
                foreach (var d in reg.drops)
                    if (d?.model != null) ocupados.Add(new Vector2(d.model.position.x, d.model.position.z));
            foreach (var z in Configuration.Instance.ZonasSeguras ?? new List<Ponto>())
                if (z.Definido) ocupados.Add(new Vector2(z.X, z.Z));
            // confere os 80 melhores com o chão de verdade (casa, pedra, água, árvores, bases)
            var melhores = new List<(float nota, float desnivel, int obst, int arv, Vector3 pos)>();
            float livre = (r + 15f) * (r + 15f);
            foreach (var c in candidatos.OrderBy(x => x.Key).Take(80))
            {
                float cx = c.Value.x, cz = c.Value.y;
                if (ocupados.Any(o => (o - c.Value).sqrMagnitude < livre)) continue;
                // safe zone do próprio mapa (o California 2 tem várias): fora dela e com folga de 30 m
                bool safeMapa = false;
                foreach (var q in Pontos(1, 12))
                {
                    var dir = q.sqrMagnitude > 0 ? q.normalized * (r + 30f) : Vector2.zero;
                    var pt = new Vector3(cx + dir.x, 0f, cz + dir.y);
                    pt.y = LevelGround.getHeight(pt) + 1f;
                    if (LevelNodes.isPointInsideSafezone(pt, out _)) { safeMapa = true; break; }
                }
                if (safeMapa) continue;
                float mn = float.MaxValue, mx = float.MinValue; int obst = 0; bool agua = false;
                foreach (var q in fino)
                {
                    var v = new Vector3(cx + q.x, LevelGround.getHeight(new Vector3(cx + q.x, 0f, cz + q.y)), cz + q.y);
                    float h = Chao(new Vector3(v.x, v.y + 5f, v.z));
                    if (h > v.y + 1.5f) obst++;
                    if (SDG.Framework.Water.WaterUtility.isPointUnderwater(new Vector3(v.x, h + .3f, v.z))) agua = true;
                    if (h < mn) mn = h; if (h > mx) mx = h;
                }
                if (agua) continue;
                var centro = new Vector3(cx, LevelGround.getHeight(new Vector3(cx, 0f, cz)), cz);
                var regs = new List<RegionCoordinate>(); var arv = new List<Transform>();
                Regions.getRegionsInRadius(centro, r, regs);
                ResourceManager.getResourcesInRadius(centro, r * r, regs, arv);
                float d = mx - mn;
                // nota: desnível pesa mais; casa/pedra no meio pesa muito; árvore pesa pouco (ela é cortada)
                melhores.Add((d + obst * 3f + arv.Count * .05f, d, obst, arv.Count, centro));
            }
            if (melhores.Count == 0) return "Por perto so achei lugar com agua, safe do mapa ou base/area ja montada. Ande pra outra regiao e tente de novo.";
            melhores = melhores.OrderBy(x => x.nota).ToList();
            // não repete lugares colados: pega os 3 melhores com 150 m de distância entre eles
            var top = new List<(float nota, float desnivel, int obst, int arv, Vector3 pos)>();
            foreach (var m in melhores)
                if (top.All(t => (t.pos - m.pos).sqrMagnitude > 150f * 150f)) { top.Add(m); if (top.Count == 3) break; }
            var best = top[0];
            adm.Player.teleportToLocationUnsafe(best.pos + Vector3.up * 1.5f, adm.Player.look.yaw);
            adm.Player.quests.replicateSetMarker(true, best.pos, "LUGAR PLANO");
            string Desc((float nota, float desnivel, int obst, int arv, Vector3 pos) m) =>
                $"desnivel {m.desnivel:0.0} m" + (m.obst > 0 ? $", {m.obst} pontos em casa/pedra" : "") + $" ({Vector3.Distance(origem, m.pos):0} m de onde voce estava)";
            var outros = top.Skip(1).Select(Desc).ToList();
            return $"Te trouxe pro lugar mais plano num raio de {raioBusca:0} m: {Desc(best)}. Marquei no mapa (M). " +
                   $"Olhe pra onde quer a entrada e use /montar {area} {modelo}." +
                   (outros.Count > 0 ? " Outras opcoes: " + string.Join(" | ", outros) + ". Use /montar " + area + " " + modelo + " procurar de novo em outro lugar pra ver mais." : "");
        }

        public string Montar(UnturnedPlayer adm, string area, int modelo, bool forcar = false, bool testar = false)
        {
            if (!ModelosMontar().TryGetValue(area + modelo, out var pecas)) return "Modelo nao existe. Use /montar safe|negro|cacador 1|2|3";
            var faltaNpc = pecas.Where(x => x.Tipo == "npc" && ushort.TryParse(x.Extra, out var n) && !(Assets.find(EAssetType.ITEM, n) is ItemBarricadeAsset))
                .Select(x => x.Extra).Distinct().ToList();
            if (faltaNpc.Count > 0) return "Os NPCs " + string.Join(", ", faltaNpc) + " ainda nao estao no mod DZ Menu. Atualize o mod no Workshop e reinicie antes de montar.";

            var centro = adm.Position;
            var frente = adm.Player.transform.forward; frente.y = 0; frente.Normalize();
            var direita = new Vector3(frente.z, 0, -frente.x);
            Vector3 Plano(float x, float y) => centro - direita * x - frente * y;
            Vector3 Mundo(float x, float y) { var v = Plano(x, y); v.y = Chao(v); return v; }
            Vector3 MundoPeca(float x, float y) { var v = Plano(x, y); v.y = ChaoPeca(v); return v; }

            // ---- mede o chão onde o modelo vai ficar ----
            float hMin = float.MaxValue, hMax = float.MinValue, raioModelo = 0f;
            Vector2 pMin = Vector2.zero, pMax = Vector2.zero;
            int obstaculos = 0;
            void Medir(float x, float y)
            {
                var v = Plano(x, y);
                float h = Chao(v);
                if (h > LevelGround.getHeight(v) + 1.5f) obstaculos++; // casa, pedra ou objeto grande no lugar
                if (h < hMin) { hMin = h; pMin = new Vector2(x, y); }
                if (h > hMax) { hMax = h; pMax = new Vector2(x, y); }
                raioModelo = Mathf.Max(raioModelo, Mathf.Sqrt(x * x + y * y));
            }
            foreach (var pc in pecas)
            {
                if (pc.Tipo == "zona") continue;
                Medir(pc.X, pc.Y);
                if (pc.Tipo == "cerca" || pc.Tipo == "parede") { Medir(pc.X2, pc.Y2); Medir((pc.X + pc.X2) / 2, (pc.Y + pc.Y2) / 2); }
            }
            var arvores = new List<Transform>();
            if (raioModelo > 0f)
            {
                var regs = new List<RegionCoordinate>();
                Regions.getRegionsInRadius(centro, raioModelo + 3f, regs);
                ResourceManager.getResourcesInRadius(centro, (raioModelo + 3f) * (raioModelo + 3f), regs, arvores);
            }
            float desnivel = hMax > hMin ? hMax - hMin : 0f;
            string Lado(Vector2 q)
            {
                // modelo: y negativo = frente (pra onde você olha), x positivo = esquerda
                if (q.sqrMagnitude < 4f) return "no centro";
                return Mathf.Abs(q.y) >= Mathf.Abs(q.x) ? (q.y < 0 ? "na frente" : "atras") : (q.x > 0 ? "na esquerda" : "na direita");
            }
            string relatorio = $"Chao: desnivel {desnivel:0.0} m (max {S.MontarDesnivelMax:0}), mais alto {Lado(pMax)}, mais baixo {Lado(pMin)}" +
                (obstaculos > 0 ? $", {obstaculos} pontos em cima de casa/pedra" : "") +
                (arvores.Count > 0 ? $", {arvores.Count} arvores (saem na montagem)" : "") + ".";
            if (testar)
                return relatorio + (desnivel <= S.MontarDesnivelMax && obstaculos == 0 ? " Lugar bom, pode montar." : " Procure um lugar mais plano e livre.");
            if (!forcar && (desnivel > S.MontarDesnivelMax || obstaculos > 3))
                return relatorio + $" Muito torto/ocupado pra montar aqui: ia ficar peca enterrada e flutuando. Use /montar {area} {modelo} procurar (te leva pro lugar mais plano por perto) ou /montar {area} {modelo} forcar.";

            // tira as árvores do lugar (cortadas; a safe não deixa elas voltarem)
            foreach (var arv in arvores)
                if (ResourceManager.tryGetRegion(arv, out var ax, out var ay, out var ai))
                    ResourceManager.ServerSetResourceDead(ax, ay, ai, Vector3.zero);
            float Yaw(float fx, float fy)
            {
                var w = -direita * fx - frente * fy;
                return w.sqrMagnitude < 1e-4f ? adm.Player.transform.eulerAngles.y : Mathf.Atan2(w.x, w.z) * Mathf.Rad2Deg;
            }

            var cfg = Configuration.Instance;
            var antes = FotoConfig(cfg);
            var colocados = new List<uint>();
            ulong dono = Id(adm);
            int nPecas = 0, nNpcs = 0, nExpostos = 0;
            var expostos = new Dictionary<string, int>();
            void Guardar(BarricadeDrop d) { if (d != null) { colocados.Add(d.instanceID); nPecas++; } }

            // ---- cercas e paredes: junta os pedaços em linhas contínuas (quebra nas quinas) e espalha as peças
            // pela linha toda com a largura real delas: sem buraco e sem uma peça em cima da outra ----
            var linhas = new List<List<PecaModelo>>();
            foreach (var pc in pecas.Where(q => q.Tipo == "cerca" || q.Tipo == "parede"))
            {
                var ult = linhas.Count > 0 ? linhas[linhas.Count - 1] : null;
                var ant = ult?[ult.Count - 1];
                bool emenda = ant != null && ant.Tipo == pc.Tipo && Mathf.Abs(ant.X2 - pc.X) < .05f && Mathf.Abs(ant.Y2 - pc.Y) < .05f;
                if (emenda)
                {
                    float ang1 = Mathf.Atan2(ant.Y2 - ant.Y, ant.X2 - ant.X), ang2 = Mathf.Atan2(pc.Y2 - pc.Y, pc.X2 - pc.X);
                    emenda = Mathf.Abs(Mathf.DeltaAngle(ang1 * Mathf.Rad2Deg, ang2 * Mathf.Rad2Deg)) < 30f;
                }
                if (emenda) ult.Add(pc); else linhas.Add(new List<PecaModelo> { pc });
            }
            foreach (var linha in linhas)
            {
                try
                {
                    ushort id = IdPeca(linha[0].Tipo, area);
                    if (id == 0) continue;
                    var pts = new List<Vector2> { new Vector2(linha[0].X, linha[0].Y) };
                    foreach (var q in linha) pts.Add(new Vector2(q.X2, q.Y2));
                    var acum = new List<float> { 0f };
                    for (int k = 1; k < pts.Count; k++) acum.Add(acum[k - 1] + Vector2.Distance(pts[k - 1], pts[k]));
                    float total = acum[acum.Count - 1];
                    if (total < .1f) continue;
                    Vector2 Em(float dist)
                    {
                        dist = Mathf.Clamp(dist, 0f, total);
                        for (int k = 1; k < pts.Count; k++)
                            if (dist <= acum[k] || k == pts.Count - 1)
                            {
                                float seg = acum[k] - acum[k - 1];
                                return seg < 1e-4f ? pts[k] : Vector2.Lerp(pts[k - 1], pts[k], (dist - acum[k - 1]) / seg);
                            }
                        return pts[pts.Count - 1];
                    }
                    // largura medida na peça de verdade; sem medida usa o raio do item ou a config
                    float yaw0 = Yaw(-(pts[1].y - pts[0].y), pts[1].x - pts[0].x) + S.MontarGiroParede;
                    float esp = MedirLargura(id, centro + Vector3.down * 3f, yaw0, dono);
                    if (esp <= 0f)
                    {
                        esp = Mathf.Max(0.5f, linha[0].Tipo == "cerca" ? S.MontarEspacoCerca : S.MontarEspacoParede);
                        if (Assets.find(EAssetType.ITEM, id) is ItemBarricadeAsset ca && ca.radius > 0.3f) esp = ca.radius * 2f;
                    }
                    int n = Mathf.Max(1, Mathf.RoundToInt(total / esp));
                    float passo = total / n;
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 p0 = Em(i * passo), p1 = Em((i + 1) * passo), pm = Em((i + .5f) * passo);
                        // virada perpendicular à linha (a largura da peça fica ao longo da linha)
                        float yaw = Yaw(-(p1.y - p0.y), p1.x - p0.x) + S.MontarGiroParede;
                        var eixo = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                        var a0 = Mundo(p0.x, p0.y); var a1 = Mundo(p1.x, p1.y); var meio = Mundo(pm.x, pm.y);
                        // altura: média das pontas, sem ficar acima do chão do meio (não flutua em buraco)
                        meio.y = Mathf.Min((a0.y + a1.y) / 2f, meio.y + .1f);
                        // inclinação ao longo da linha (ladeira), no máximo 25 graus
                        var d = a1 - a0; float hor = new Vector2(d.x, d.z).magnitude;
                        float ang = hor > .1f ? Mathf.Clamp(Mathf.Atan2(d.y, hor) * Mathf.Rad2Deg, -25f, 25f) : 0f;
                        // sinal certo: o lado da peça que aponta pra a1 tem que subir junto com o chão
                        if (Vector3.Dot(eixo, new Vector3(d.x, 0, d.z)) < 0) ang = -ang;
                        var teste = Quaternion.AngleAxis(ang, Quaternion.Euler(0f, yaw, 0f) * Vector3.forward) * eixo;
                        if (Mathf.Abs(ang) > .5f && Mathf.Sign(teste.y) != Mathf.Sign(ang)) ang = -ang;
                        Guardar(Colocar(id, meio, yaw, dono, false, ang));
                    }
                }
                catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            }

            foreach (var pc in pecas)
            {
                try
                {
                    switch (pc.Tipo)
                    {
                        case "zona":
                            if (area == "safe")
                                cfg.ZonasSeguras.Add(new Ponto("Safe " + (cfg.ZonasSeguras.Count + 1), pc.X) { X = centro.x, Y = centro.y, Z = centro.z });
                            break;
                        case "cerca":
                        case "parede":
                            break; // colocadas antes, linha por linha (ColocarLinhas)
                        case "npc":
                        {
                            if (!ushort.TryParse(pc.Extra, out var id)) break;
                            var pos = MundoPeca(pc.X, pc.Y);
                            var d = Colocar(id, pos, Yaw(pc.X2, pc.Y2), dono, true);
                            if (d == null) break;
                            Guardar(d); nNpcs++;
                            if (id == 51249) cfg.Atms.Add(new Ponto("ATM " + (cfg.Atms.Count + 1), 4f) { X = pos.x, Y = pos.y, Z = pos.z });
                            break;
                        }
                        case "veiculo":
                        {
                            // carro comprado na garagem da safe nasce aqui
                            var v = Mundo(pc.X, pc.Y);
                            var loja = cfg.Lojas.FirstOrDefault(l => l.Id == "garagem");
                            if (loja != null && area == "safe") { loja.VX = v.x; loja.VY = v.y; loja.VZ = v.z; }
                            break;
                        }
                        case "vaga":
                        {
                            var v = Mundo(pc.X, pc.Y); float r = Yaw(pc.X2, pc.Y2);
                            Guardar(Colocar(IdPeca("vaga", area), v, r, dono));
                            (cfg.Vagas = cfg.Vagas ?? new List<Vaga>()).Add(new Vaga { Tipo = "carro", X = v.x, Y = v.y, Z = v.z, R = r });
                            break;
                        }
                        case "heliponto":
                        {
                            var v = Mundo(pc.X, pc.Y);
                            Guardar(Colocar(IdPeca("heliponto", area), v, Yaw(0, 1), dono));
                            // safe e negro: heli da garagem sai aqui (no caçador é só pouso)
                            if (area != "cacador") (cfg.Vagas = cfg.Vagas ?? new List<Vaga>()).Add(new Vaga { Tipo = "heli", X = v.x, Y = v.y, Z = v.z, R = Yaw(0, 1) });
                            // no mercado negro o heli comprado nasce no heliponto
                            var loja = cfg.Lojas.FirstOrDefault(l => l.Id == (area == "safe" ? "hangar" : "negro"));
                            if (loja != null && (area == "negro" || area == "safe")) { loja.VX = v.x; loja.VY = v.y; loja.VZ = v.z; }
                            break;
                        }
                        case "pista_inicio":
                        {
                            var v = Mundo(pc.X, pc.Y);
                            var loja = cfg.Lojas.FirstOrDefault(l => l.Id == (area == "safe" ? "hangar" : "negro"));
                            if (loja != null && (area == "negro" || area == "safe")) { loja.AX = v.x; loja.AY = v.y; loja.AZ = v.z; loja.AR = Yaw(pc.X2, pc.Y2); }
                            (cfg.Vagas = cfg.Vagas ?? new List<Vaga>()).Add(new Vaga { Tipo = "aviao", X = v.x, Y = v.y, Z = v.z, R = Yaw(pc.X2, pc.Y2) });
                            break;
                        }
                        case "bandeira":
                        {
                            var pos = MundoPeca(pc.X, pc.Y); float yaw = Yaw(pc.X2, pc.Y2);
                            var d = Colocar(IdPeca("bandeira", area), pos, yaw, dono);
                            if (d == null)
                            {
                                d = Colocar(IdPeca("bandeira_placa", area), pos, yaw, dono);
                                if (d?.interactable is InteractableSign placa) BarricadeManager.ServerSetSignText(placa, "DZ BRASIL");
                            }
                            Guardar(d);
                            break;
                        }
                        default:
                        {
                            var d = Colocar(IdPeca(pc.Tipo, area), MundoPeca(pc.X, pc.Y), Yaw(pc.X2, pc.Y2), dono);
                            if (d == null) break;
                            Guardar(d);
                            if (d.interactable is InteractableFire fogo) BarricadeManager.ServerSetFireLit(fogo, true);
                            if (d.interactable is InteractableStorage bau && !string.IsNullOrEmpty(pc.Extra))
                            {
                                // enfeite: coloca um item da loja no mostruário (ninguém consegue abrir)
                                var lista = ItensExpostos(pc.Extra);
                                if (lista.Count == 0) break;
                                expostos.TryGetValue(pc.Extra, out var k);
                                expostos[pc.Extra] = k + 1;
                                var item = new Item(lista[k % lista.Count], EItemOrigin.ADMIN);
                                if (bau.items.tryAddItem(item))
                                {
                                    nExpostos++;
                                    if (bau.isDisplay) BarricadeManager.sendStorageDisplay(d.model, item, 0, 0, "", "");
                                    bau.rebuildState();
                                }
                            }
                            break;
                        }
                    }
                }
                catch (Exception e) { Rocket.Core.Logging.Logger.LogException(e); }
            }

            // lojas da área apontam pro centro (o NPC mais perto do tipo abre a loja)
            var ids = area == "safe" ? new[] { "armas", "comida", "roupas", "remedios", "equipamentos", "garagem", "construcao" }
                : new[] { area };
            foreach (var idLoja in ids)
            {
                var l = cfg.Lojas.FirstOrDefault(x => x.Id == idLoja);
                if (l == null) continue;
                l.X = centro.x; l.Y = centro.y; l.Z = centro.z;
                if (l.Raio < 30) l.Raio = 30;
            }
            Configuration.Save();
            pecasMontadas = null;
            try
            {
                File.AppendAllText(ArqMontagens, $"{DateTime.Now:yyyy-MM-dd HH:mm}\t{area}{modelo}\t{string.Join(",", colocados)}\t" +
                    $"{centro.x:0.0} {centro.y:0.0} {centro.z:0.0}\t{DiffConfig(antes, cfg)}{Environment.NewLine}");
            }
            catch { }
            LogAdmin(adm, $"MONTOU {area} modelo {modelo} em {centro.x:0} {centro.z:0}: {nPecas} pecas, {nNpcs} NPCs, {nExpostos} itens expostos");
            return $"Montado {area} modelo {modelo}: {nPecas} pecas ({nNpcs} NPCs, {nExpostos} itens nos mostruarios). Desfazer: /desmontar";
        }

        /// <summary>/bomba: coloca uma bomba de gasolina (combustível infinito, não quebra) onde o dono está, virada pra onde ele olha.</summary>
        public string ColocarBomba(UnturnedPlayer adm)
        {
            var id = IdPeca("bomba", NaSafe(adm.Position) ? "safe" : "cidade");
            if (id == 0) return "Nao achei a bomba de gasolina (pack DZ ou Fuel Drum).";
            var pos = adm.Position + adm.Player.look.aim.forward.normalized * 2f;
            pos.y = Chao(pos);
            var d = Colocar(id, pos, adm.Player.look.yaw + 180f, Id(adm));
            if (d == null) return "Nao consegui colocar aqui. Tente num chao plano.";
            pecasMontadas = null;
            try
            {
                File.AppendAllText(ArqMontagens, $"{DateTime.Now:yyyy-MM-dd HH:mm}\tbomba\t{d.instanceID}\t" +
                    $"{pos.x:0.0} {pos.y:0.0} {pos.z:0.0}{Environment.NewLine}");
            }
            catch { }
            LogAdmin(adm, $"BOMBA de gasolina em {pos.x:0} {pos.z:0}");
            return "Bomba colocada (gasolina infinita, nao quebra). Desfazer: /desmontar";
        }

        public string Desmontar()
        {
            if (!File.Exists(ArqMontagens)) return "Nada pra desfazer.";
            var linhas = File.ReadAllLines(ArqMontagens).Where(l => l.Trim() != "").ToList();
            if (linhas.Count == 0) return "Nada pra desfazer.";
            var ult = linhas.Last().Split('\t');
            var ids = new HashSet<uint>((ult.Length > 2 ? ult[2] : "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => uint.TryParse(x, out var v) ? v : 0).Where(v => v != 0));
            int tirados = 0;
            for (byte x = 0; x < Regions.WORLD_SIZE; x++)
                for (byte y = 0; y < Regions.WORLD_SIZE; y++)
                {
                    var reg = BarricadeManager.regions[x, y];
                    foreach (var d in reg.drops.Where(d => ids.Contains(d.instanceID)).ToList())
                    {
                        // esvazia antes pra não cair item no chão
                        if (d.interactable is InteractableStorage bau) bau.items.clear();
                        BarricadeManager.destroyBarricade(d, x, y, ushort.MaxValue); tirados++;
                    }
                }
            linhas.RemoveAt(linhas.Count - 1);
            pecasMontadas = null;
            File.WriteAllLines(ArqMontagens, linhas);
            string extra = DesfazerConfig(ult);
            return $"Desfeito: {tirados} pecas tiradas ({ult[1]}){extra}.";
        }
    }

    public class CmdMontar : ComandoSistema
    {
        public override string Name => "montar";
        public override string Help => "Monta safe, mercado negro ou cacador em volta de voce (dono).";
        public override string Syntax => "<safe|negro|cacador> <1|2|3> [testar|forcar|procurar]";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.EhDono(p) && !p.IsAdmin) { Chat.Erro(p, "So o dono usa isso."); return; }
            if (a.Length < 1)
            {
                Chat.Info(p, "Fique no centro, olhe pra onde quer a ENTRADA e use: /montar safe 1 | /montar negro 2 | /montar cacador 3. Antes, /montar safe 1 procurar acha o lugar mais plano e /montar safe 1 testar mede o chao.");
                return;
            }
            var area = a[0].ToLowerInvariant();
            if (area == "caca" || area == "caçador") area = "cacador";
            if (area == "mercadonegro") area = "negro";
            int modelo = a.Skip(1).Select(x => int.TryParse(x, out var m) ? m : 0).FirstOrDefault(m => m > 0);
            if (modelo == 0) modelo = 1;
            bool forcar = a.Any(x => x.Equals("forcar", StringComparison.OrdinalIgnoreCase) || x.Equals("forçar", StringComparison.OrdinalIgnoreCase));
            bool testar = a.Any(x => x.Equals("testar", StringComparison.OrdinalIgnoreCase) || x.Equals("teste", StringComparison.OrdinalIgnoreCase));
            P.ModelosRecarregar();
            if (a.Any(x => x.Equals("procurar", StringComparison.OrdinalIgnoreCase) || x.Equals("achar", StringComparison.OrdinalIgnoreCase)))
            {
                Chat.Info(p, "Procurando o lugar mais plano em volta (ate 1,5 km)...");
                Chat.Ok(p, P.ProcurarLugar(p, area, modelo));
                return;
            }
            Chat.Ok(p, P.Montar(p, area, modelo, forcar, testar));
        }
    }

    public partial class Plugin
    {
        public void ModelosRecarregar() => modelosMontar = null;
    }

    public class CmdDesmontar : ComandoSistema
    {
        public override string Name => "desmontar";
        public override string Help => "Desfaz o ultimo /montar (dono).";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.EhDono(p) && !p.IsAdmin) { Chat.Erro(p, "So o dono usa isso."); return; }
            Chat.Info(p, P.Desmontar());
        }
    }

    public class CmdBomba : ComandoSistema
    {
        public override string Name => "bomba";
        public override string Help => "Coloca uma bomba de gasolina na sua frente (dono).";
        protected override void Rodar(UnturnedPlayer p, string[] a)
        {
            if (!P.EhDono(p) && !p.IsAdmin) { Chat.Erro(p, "So o dono usa isso."); return; }
            Chat.Info(p, P.ColocarBomba(p));
        }
    }

    public partial class Plugin
    {
        // ---------- desfazer o que o /montar registrou no config ----------

        class FotoCfg { public HashSet<Ponto> Zonas, Atms; public HashSet<Vaga> Vagas; public Dictionary<string, string> Lojas; }

        static string PosLoja(Loja l) => string.Join(",", new[] { l.X, l.Y, l.Z, l.Raio, l.VX, l.VY, l.VZ, l.AX, l.AY, l.AZ, l.AR }
            .Select(v => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)));

        static FotoCfg FotoConfig(Config cfg) => new FotoCfg
        {
            Zonas = new HashSet<Ponto>(cfg.ZonasSeguras),
            Atms = new HashSet<Ponto>(cfg.Atms),
            Vagas = new HashSet<Vaga>(cfg.Vagas ?? new List<Vaga>()),
            Lojas = cfg.Lojas.Where(l => l.Id != null).GroupBy(l => l.Id).ToDictionary(g => g.Key, g => PosLoja(g.First())),
        };

        static string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>O que mudou: "z=Safe 1|a=ATM 1|v=carro:x:z|l=armas:posAntiga" separado por ';'.</summary>
        static string DiffConfig(FotoCfg a, Config cfg)
        {
            var r = new List<string>();
            r.AddRange(cfg.ZonasSeguras.Where(z => !a.Zonas.Contains(z)).Select(z => "z=" + z.Nome));
            r.AddRange(cfg.Atms.Where(x => !a.Atms.Contains(x)).Select(x => "a=" + x.Nome));
            r.AddRange((cfg.Vagas ?? new List<Vaga>()).Where(v => !a.Vagas.Contains(v)).Select(v => $"v={v.Tipo}:{F(v.X)}:{F(v.Z)}"));
            foreach (var l in cfg.Lojas)
                if (l.Id != null && a.Lojas.TryGetValue(l.Id, out var pos) && pos != PosLoja(l)) r.Add($"l={l.Id}:{pos}");
            return string.Join(";", r.Select(x => x.Replace("\t", " ").Replace(";", ",")));
        }

        string DesfazerConfig(string[] linha)
        {
            var cfg = Configuration.Instance;
            int zonas = 0, atms = 0, vagas = 0, lojas = 0;
            if (linha.Length > 4 && linha[4].Trim() != "")
            {
                foreach (var it in linha[4].Split(';'))
                {
                    int eq = it.IndexOf('='); if (eq < 1) continue;
                    var tipo = it.Substring(0, eq); var val = it.Substring(eq + 1);
                    if (tipo == "z") zonas += cfg.ZonasSeguras.RemoveAll(z => z.Nome == val);
                    else if (tipo == "a") atms += cfg.Atms.RemoveAll(x => x.Nome == val);
                    else if (tipo == "v" && cfg.Vagas != null)
                    {
                        var p = val.Split(':');
                        if (p.Length == 3 && float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var vx)
                            && float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var vz))
                            vagas += cfg.Vagas.RemoveAll(v => v.Tipo == p[0] && Mathf.Abs(v.X - vx) < 0.01f && Mathf.Abs(v.Z - vz) < 0.01f);
                    }
                    else if (tipo == "l")
                    {
                        int dp = val.IndexOf(':'); if (dp < 1) continue;
                        var l = cfg.Lojas.FirstOrDefault(x => x.Id == val.Substring(0, dp));
                        var n = val.Substring(dp + 1).Split(',').Select(x => float.TryParse(x, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 0f).ToArray();
                        if (l == null || n.Length < 11) continue;
                        l.X = n[0]; l.Y = n[1]; l.Z = n[2]; l.Raio = n[3]; l.VX = n[4]; l.VY = n[5]; l.VZ = n[6]; l.AX = n[7]; l.AY = n[8]; l.AZ = n[9]; l.AR = n[10];
                        lojas++;
                    }
                }
            }
            else if (linha.Length > 3 && linha[1].StartsWith("safe"))
            {
                // montagem antiga (sem registro): tira a safe zone criada no mesmo centro
                var c = linha[3].Split(' ').Select(x => float.TryParse(x, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : float.NaN).ToArray();
                if (c.Length == 3) zonas += cfg.ZonasSeguras.RemoveAll(z => Mathf.Abs(z.X - c[0]) < 0.2f && Mathf.Abs(z.Z - c[2]) < 0.2f);
            }
            if (zonas + atms + vagas + lojas == 0) return "";
            Configuration.Save();
            var partes = new List<string>();
            if (zonas > 0) partes.Add(zonas + " safe zone");
            if (atms > 0) partes.Add(atms + " ATM");
            if (vagas > 0) partes.Add(vagas + " vagas");
            if (lojas > 0) partes.Add(lojas + " lojas voltaram pro lugar antigo");
            return ", removido: " + string.Join(", ", partes);
        }
    }
}
