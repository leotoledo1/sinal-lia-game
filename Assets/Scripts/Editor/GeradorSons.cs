using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Sintetiza os sons do jogo (sem precisar baixar nada) e salva em Assets/Audio/*.wav.
// Só cria os que não existem: pra usar um som gravado de verdade, substitua o .wav com o mesmo nome.
public static class GeradorSons
{
    public const string PASTA = "Assets/Audio";
    const int SR = 44100;
    static System.Random rnd;

    [MenuItem("Sinal da Lia/Recriar Sons (apaga e gera de novo)")]
    public static void Recriar()
    {
        GerarTodos(true);
        EditorUtility.DisplayDialog("Sinal da Lia", "Sons gerados em Assets/Audio.", "OK");
    }

    public static void GerarTodos(bool sobrescrever)
    {
        Directory.CreateDirectory(PASTA);
        // tiros: duração, decaimento do estalo, grave inicial->final, decaimento do grave, cauda (eco)
        Gerar("tiro_pistola", sobrescrever, 1, () => Tiro(0.5f, 0.03f, 150f, 60f, 0.07f, 0.18f, 0.9f));
        Gerar("tiro_escopeta", sobrescrever, 2, () => Tiro(0.9f, 0.07f, 95f, 40f, 0.12f, 0.35f, 1.2f));
        Gerar("tiro_rifle", sobrescrever, 3, () => Tiro(0.35f, 0.022f, 170f, 70f, 0.05f, 0.12f, 0.85f));
        Gerar("tiro_sniper", sobrescrever, 4, () => Tiro(1.6f, 0.05f, 75f, 32f, 0.16f, 0.6f, 1.3f));
        Gerar("recarga", sobrescrever, 5, Recarga);
        Gerar("vazio", sobrescrever, 6, () => { var s = Vazio(0.2f); Clique(s, 0.01f, 1f, 2600f); return s; });
        for (int i = 0; i < 4; i++) { int k = i; Gerar("passo_" + (i + 1), sobrescrever, 10 + i, () => Passo(k)); }
        for (int i = 0; i < 4; i++) { int k = i; Gerar("zumbi_gemido_" + (i + 1), sobrescrever, 20 + i, () => Voz(75f + k * 9f, 1.3f + k * 0.2f, 0.25f, 0.35f, 22f + k * 4f, 0.25f)); }
        Gerar("zumbi_ataque", sobrescrever, 30, () => Voz(140f, 0.55f, 0.1f, 0.7f, 38f, 0.55f));
        Gerar("zumbi_morte", sobrescrever, 31, () => Voz(115f, 1.4f, 0.55f, 0.4f, 18f, 0.3f));
        Gerar("impacto_carne", sobrescrever, 32, ImpactoCarne);
        Gerar("impacto_parede", sobrescrever, 33, ImpactoParede);
        Gerar("acerto", sobrescrever, 34, Acerto);
        Gerar("dano_jogador", sobrescrever, 35, DanoJogador);
        Gerar("radio_chiado", sobrescrever, 36, Chiado);
        Gerar("pegar_item", sobrescrever, 37, () => Bipe(880f, 1320f, 0.08f));
        Gerar("cura", sobrescrever, 38, () => Bipe(523f, 784f, 0.18f));
        Gerar("vento_loop", sobrescrever, 40, () => Loop(12f, Vento));
        Gerar("noite_loop", sobrescrever, 41, () => Loop(16f, Noite));
        Gerar("fogo_loop", sobrescrever, 42, () => Loop(4f, Fogo));
        // música (semitons a partir de Lá 110 Hz)
        int[] Am = { 0, 3, 7, 12 }, F = { -4, 0, 3, 8 }, C = { 3, 7, 10, 15 }, G = { -2, 2, 5, 10 };
        int[] Em = { -5, -2, 2, 7 }, Dm = { -7, -4, 0, 5 }, Bb = { 1, 5, 8, 13 }, Fm = { -4, -1, 3, 8 };
        Gerar("musica_menu", sobrescrever, 50, () => Musica(60f, new[] { Am, F, C, G }, 4, 1f, 0f, 0f, false, 0.35f));
        Gerar("musica_dia", sobrescrever, 51, () => Musica(70f, new[] { Am, F, Dm, Em, Am, F, Em, Em }, 4, 1f, 0.25f, 0f, false, 0.45f));
        Gerar("musica_noite", sobrescrever, 52, () => Musica(88f, new[] { Am, Bb, Am, Fm }, 8, 0.8f, 1f, 0.25f, true, 0.8f));
        Gerar("musica_final", sobrescrever, 53, () => Musica(118f, new[] { Am, F, G, Em }, 8, 0.75f, 1f, 0.45f, false, 1f));
        Gerar("vitoria", sobrescrever, 54, () => Acorde(new[] { 3, 7, 10, 15, 19 }, 4.5f, 1.2f));
        Gerar("game_over", sobrescrever, 55, () => Acorde(new[] { -12, -9, -5, -4 }, 4f, 0.05f));
        Gerar("helicoptero_loop", sobrescrever, 56, Helicoptero);
        AssetDatabase.Refresh();
    }

    public static AudioClip Carregar(string nome) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{PASTA}/{nome}.wav");

    static void Gerar(string nome, bool sobrescrever, int semente, Func<float[]> f)
    {
        string path = $"{PASTA}/{nome}.wav";
        if (!sobrescrever && File.Exists(path)) return;
        rnd = new System.Random(semente);
        var s = f();
        Normalizar(s, 0.9f);
        SalvarWav(path, s);
    }

    // receitas

    static float[] Tiro(float dur, float tauEstalo, float f0, float f1, float tauGrave, float tauCauda, float ganho)
    {
        var s = Vazio(dur);
        float lp = 0f, lpCauda = 0f, fase = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            float n = Ruido();
            // estalo: ruído que vai ficando mais abafado
            float corte = Mathf.Lerp(9000f, 1200f, Mathf.Clamp01(t / (tauEstalo * 4f)));
            lp += (n - lp) * Coef(corte);
            float estalo = lp * Mathf.Exp(-t / tauEstalo);
            // grave: seno descendo de f0 pra f1
            float f = f1 + (f0 - f1) * Mathf.Exp(-t / (tauGrave * 0.7f));
            fase += 2f * Mathf.PI * f / SR;
            float grave = Mathf.Sin(fase) * Mathf.Exp(-t / tauGrave);
            // cauda: eco abafado da rua
            lpCauda += (n - lpCauda) * Coef(700f);
            float cauda = lpCauda * Mathf.Exp(-t / tauCauda) * 0.5f;
            float ataque = Mathf.Clamp01(t / 0.0008f);
            s[i] = Satura((estalo * 1.4f + grave * 1.1f + cauda) * ataque * ganho);
        }
        return s;
    }

    static float[] Recarga()
    {
        var s = Vazio(0.9f);
        Clique(s, 0.05f, 0.9f, 1800f);   // tira o pente
        Raspar(s, 0.22f, 0.12f, 0.35f);
        Clique(s, 0.42f, 1f, 2200f);    // põe o pente
        Clique(s, 0.68f, 0.8f, 1500f);  // engatilha
        Raspar(s, 0.7f, 0.08f, 0.4f);
        Clique(s, 0.8f, 1f, 2600f);
        return s;
    }

    static float[] Passo(int variacao)
    {
        var s = Vazio(0.22f);
        float lp = 0f, lp2 = 0f;
        float corte = 450f + variacao * 120f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            float n = Ruido();
            lp += (n - lp) * Coef(corte);
            lp2 += (n - lp2) * Coef(3500f);
            float env = Mathf.Clamp01(t / 0.004f) * Mathf.Exp(-t / 0.045f);
            float areia = (n - lp2) * Mathf.Exp(-t / 0.02f) * 0.12f;
            s[i] = lp * env * 2.5f + areia;
        }
        return s;
    }

    // "voz" de zumbi: dente-de-serra grave passando por filtros de formante (uh/ah) + rouquidão
    static float[] Voz(float f0, float dur, float queda, float ruido, float rouquidao, float profRouq)
    {
        var s = Vazio(dur);
        var f1 = new Biquad(520f, 4f); var f2 = new Biquad(950f, 5f); var f3 = new Biquad(2400f, 6f);
        float fase = 0f, lp = 0f;
        float vib = (float)rnd.NextDouble() * 6f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR, u = t / dur;
            float f = f0 * (1f - queda * u) * (1f + 0.04f * Mathf.Sin(2f * Mathf.PI * 4.5f * t + vib)) * (1f + 0.02f * Ruido());
            fase += f / SR;
            if (fase > 1f) fase -= 1f;
            float serra = fase * 2f - 1f;
            float n = Ruido();
            lp += (n - lp) * Coef(1800f);
            float fonte = serra * (1f - ruido) + lp * ruido * 2f;
            float voz = f1.Proc(fonte) + f2.Proc(fonte) * 0.6f + f3.Proc(fonte) * 0.25f;
            float rouq = 1f - profRouq * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * rouquidao * t));
            float env = Mathf.Clamp01(u / 0.2f) * Mathf.Clamp01((1f - u) / 0.35f);
            s[i] = Satura(voz * rouq * env * 3f);
        }
        return s;
    }

    static float[] ImpactoCarne()
    {
        var s = Vazio(0.25f);
        float lp = 0f, fase = 0f;
        var molhado = new Biquad(1400f, 2f);
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            float n = Ruido();
            lp += (n - lp) * Coef(380f);
            fase += 2f * Mathf.PI * 85f / SR;
            s[i] = lp * Mathf.Exp(-t / 0.035f) * 2f + Mathf.Sin(fase) * Mathf.Exp(-t / 0.04f) * 0.8f + molhado.Proc(n) * Mathf.Exp(-t / 0.06f) * 0.5f;
        }
        return s;
    }

    static float[] ImpactoParede()
    {
        var s = Vazio(0.2f);
        float lp = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            float n = Ruido();
            lp += (n - lp) * Coef(2500f);
            s[i] = (n - lp * 0.5f) * Mathf.Exp(-t / 0.012f) + Mathf.Sin(2f * Mathf.PI * 1700f * t) * Mathf.Exp(-t / 0.03f) * 0.2f;
        }
        return s;
    }

    static float[] Acerto()
    {
        var s = Vazio(0.07f);
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            s[i] = (Mathf.Sin(2f * Mathf.PI * 2600f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 3900f * t)) * Mathf.Exp(-t / 0.015f);
        }
        return s;
    }

    // música

    static float Freq(int semitom) => 110f * Mathf.Pow(2f, semitom / 12f);

    // trilha em loop perfeito: pad de acordes (com baixo) + bumbo/batida de coração + chimbal opcionais.
    // Tudo é escrito "dando a volta" no fim do buffer, então o loop não estala.
    static float[] Musica(float bpm, int[][] acordes, int batidasPorAcorde, float volPad, float volBumbo, float volChimbal, bool coracao, float brilho)
    {
        float batida = 60f / bpm, durAcorde = batida * batidasPorAcorde;
        int n = (int)(durAcorde * acordes.Length * SR);
        var s = new float[n];
        int len = (int)(durAcorde * SR), fade = (int)(0.7f * SR);
        for (int a = 0; a < acordes.Length; a++)
        {
            int ini = a * len;
            var notas = new System.Collections.Generic.List<(float f, float vol, float harm)>();
            foreach (int st in acordes[a]) notas.Add((Freq(st), 0.22f * volPad, brilho));
            notas.Add((Freq(acordes[a][0] - 12), 0.35f * volPad, 0.1f)); // baixo
            foreach (var nota in notas)
                for (int v = 0; v < 2; v++)
                {
                    double inc = 2.0 * Math.PI * nota.f * (1.0 + (v == 0 ? -0.002 : 0.002)) / SR;
                    double fase = rnd.NextDouble() * 6.283;
                    for (int i = -fade; i < len + fade; i++)
                    {
                        float env = i < fade ? (i + fade) / (2f * fade) : (i > len - fade ? (len + fade - i) / (2f * fade) : 1f);
                        double ph = fase + inc * i;
                        float x = (float)(Math.Sin(ph) + nota.harm * (0.35 * Math.Sin(2.0 * ph) + 0.18 * Math.Sin(3.0 * ph)));
                        s[((ini + i) % n + n) % n] += x * env * nota.vol;
                    }
                }
        }
        // suaviza o pad (passa-baixa) duas voltas, pra emendar o fim com o começo
        float y = 0f, k = Coef(1400f + 1600f * brilho);
        for (int volta = 0; volta < 2; volta++)
            for (int i = 0; i < n; i++) { y += k * (s[i] - y); if (volta == 1) s[i] = y; }

        int passos = Mathf.RoundToInt(n / (batida * SR));
        for (int b = 0; b < passos; b++)
        {
            int t0 = (int)(b * batida * SR);
            if (volBumbo > 0f && (coracao || bpm > 100f || b % 2 == 0))
            {
                Bumbo(s, t0, volBumbo, n);
                if (coracao) Bumbo(s, t0 + (int)(0.28f * batida * SR), volBumbo * 0.6f, n);
            }
            if (volChimbal > 0f) Chimbal(s, t0 + (int)(0.5f * batida * SR), volChimbal, n);
        }
        return s;
    }

    static void Bumbo(float[] s, int inicio, float vol, int n)
    {
        double fase = 0;
        int dur = (int)(0.35f * SR);
        for (int i = 0; i < dur; i++)
        {
            float t = i / (float)SR;
            fase += 2.0 * Math.PI * (45f + 55f * Mathf.Exp(-t / 0.04f)) / SR;
            s[(inicio + i) % n] += (float)Math.Sin(fase) * Mathf.Exp(-t / 0.12f) * 0.9f * vol;
        }
    }

    static void Chimbal(float[] s, int inicio, float vol, int n)
    {
        float anterior = 0f;
        int dur = (int)(0.06f * SR);
        for (int i = 0; i < dur; i++)
        {
            float r = Ruido(), agudo = r - anterior; // passa-alta simples
            anterior = r;
            s[(inicio + i) % n] += agudo * Mathf.Exp(-i / (0.015f * SR)) * 0.25f * vol;
        }
    }

    // acorde único que cresce e some (vinhetas de vitória / game over)
    static float[] Acorde(int[] semitons, float dur, float ataque)
    {
        var s = Vazio(dur);
        foreach (int st in semitons)
        {
            double inc = 2.0 * Math.PI * Freq(st) / SR;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Clamp01(t / ataque) * Mathf.Clamp01((dur - t) / (dur * 0.6f));
                double ph = inc * i;
                s[i] += (float)(Math.Sin(ph) + 0.3 * Math.Sin(2.0 * ph) + 0.12 * Math.Sin(3.0 * ph)) * env * 0.25f;
            }
        }
        return s;
    }

    // rotor: 20 batidas por segundo (pás) + ronco grave + turbina, em loop de 2 s
    static float[] Helicoptero()
    {
        int n = 2 * SR;
        var s = new float[n];
        for (int p = 0; p < 40; p++)
        {
            int ini = p * n / 40;
            float y = 0f, k = Coef(500f);
            for (int i = 0; i < (int)(0.045f * SR); i++)
            {
                y += k * (Ruido() - y);
                float t = i / (float)SR;
                s[(ini + i) % n] += (y * 2.2f + 0.6f * Mathf.Sin(2f * Mathf.PI * 60f * t)) * Mathf.Exp(-t / 0.018f);
            }
        }
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            s[i] += 0.25f * Mathf.Sin(2f * Mathf.PI * 38f * t) + 0.05f * Mathf.Sin(2f * Mathf.PI * 1100f * t);
        }
        return s;
    }

    // dois bipes subindo (pegar item, curar)
    static float[] Bipe(float f0, float f1, float dur)
    {
        var s = Vazio(dur * 2f);
        int meio = (int)(dur * SR);
        for (int i = 0; i < s.Length; i++)
        {
            float t = (i % meio) / (float)SR;
            float f = i < meio ? f0 : f1;
            s[i] = (Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * f * t)) * Mathf.Exp(-t / (dur * 0.35f));
        }
        return s;
    }

    static float[] DanoJogador()
    {
        var baque = Vazio(0.4f);
        float fase = 0f;
        for (int i = 0; i < baque.Length; i++)
        {
            float t = i / (float)SR;
            fase += 2f * Mathf.PI * (60f + 40f * Mathf.Exp(-t / 0.05f)) / SR;
            baque[i] = Mathf.Sin(fase) * Mathf.Exp(-t / 0.09f);
        }
        var grito = Voz(125f, 0.35f, 0.2f, 0.3f, 30f, 0.3f);
        for (int i = 0; i < baque.Length && i < grito.Length; i++) baque[i] += grito[i] * 0.5f;
        return baque;
    }

    static float[] Chiado()
    {
        var s = Vazio(1f);
        var banda = new Biquad(1300f, 0.8f);
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            float n = banda.Proc(Ruido());
            if (rnd.NextDouble() < 0.0015) n += Ruido() * 6f; // estalos
            float env = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((1f - t) / 0.2f);
            s[i] = n * env * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 7f * t));
        }
        return s;
    }

    static float Vento(int i, float dur)
    {
        float t = i / (float)SR;
        float corte = 250f + 450f * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.09f * t)) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.23f * t + 1f));
        estadoA += (Ruido() - estadoA) * Coef(corte);
        float amp = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.15f * t);
        return estadoA * amp * 3f;
    }

    static float Noite(int i, float dur)
    {
        // drone grave com frequências que fecham ciclo inteiro em 16 s + ruído abafado
        float t = i / (float)SR;
        float d = Mathf.Sin(2f * Mathf.PI * 55f * t) + 0.8f * Mathf.Sin(2f * Mathf.PI * 55.125f * t)
                + 0.45f * Mathf.Sin(2f * Mathf.PI * 82.5f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * 110.0625f * t);
        float lfo = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * 0.125f * t);
        estadoA += (Ruido() - estadoA) * Coef(180f);
        return d * 0.25f * lfo + estadoA * 1.5f;
    }

    static float Fogo(int i, float dur)
    {
        estadoA += (Ruido() - estadoA) * Coef(600f);
        float estalo = 0f;
        if (rnd.NextDouble() < 0.0006) estadoB = (float)rnd.NextDouble() * 0.8f + 0.2f;
        estadoB *= 0.993f;
        estalo = Ruido() * estadoB;
        return estadoA * 1.2f + estalo * 0.8f;
    }

    static float estadoA, estadoB;

    // gera dur + 1 s e cruza o fim com o começo: o loop fica sem emenda
    static float[] Loop(float dur, Func<int, float, float> amostra)
    {
        estadoA = estadoB = 0f;
        int n = (int)(dur * SR), fade = SR;
        var bruto = new float[n + fade];
        for (int i = 0; i < bruto.Length; i++) bruto[i] = amostra(i, dur);
        var s = new float[n];
        for (int i = 0; i < n; i++) s[i] = bruto[i];
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            s[i] = bruto[i] * k + bruto[n + i] * (1f - k);
        }
        return s;
    }

    // blocos

    static float[] Vazio(float dur) => new float[(int)(dur * SR)];
    static float Ruido() => (float)(rnd.NextDouble() * 2.0 - 1.0);
    static float Coef(float corte) => 1f - Mathf.Exp(-2f * Mathf.PI * corte / SR);
    static float Satura(float x) => (float)Math.Tanh(x);

    static void Clique(float[] s, float inicio, float vol, float tom)
    {
        int i0 = (int)(inicio * SR);
        float lp = 0f;
        for (int i = i0; i < s.Length && i < i0 + SR / 10; i++)
        {
            float t = (i - i0) / (float)SR;
            float n = Ruido();
            lp += (n - lp) * Coef(3000f);
            s[i] += ((n - lp) * Mathf.Exp(-t / 0.004f) + Mathf.Sin(2f * Mathf.PI * tom * t) * Mathf.Exp(-t / 0.02f) * 0.4f) * vol;
        }
    }

    static void Raspar(float[] s, float inicio, float dur, float vol)
    {
        int i0 = (int)(inicio * SR), n = (int)(dur * SR);
        var banda = new Biquad(2800f, 1.5f);
        for (int i = 0; i < n && i0 + i < s.Length; i++)
        {
            float u = i / (float)n;
            s[i0 + i] += banda.Proc(Ruido()) * Mathf.Sin(Mathf.PI * u) * vol;
        }
    }

    class Biquad
    {
        float b0, b2, a1, a2, x1, x2, y1, y2;
        public Biquad(float freq, float q)
        {
            float w = 2f * Mathf.PI * freq / SR, alpha = Mathf.Sin(w) / (2f * q), a0 = 1f + alpha;
            b0 = alpha / a0; b2 = -alpha / a0; a1 = -2f * Mathf.Cos(w) / a0; a2 = (1f - alpha) / a0;
        }
        public float Proc(float x)
        {
            float y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x; y2 = y1; y1 = y;
            return y;
        }
    }

    static void Normalizar(float[] s, float pico)
    {
        float max = 0f;
        foreach (var v in s) max = Mathf.Max(max, Mathf.Abs(v));
        if (max < 1e-6f) return;
        float k = pico / max;
        for (int i = 0; i < s.Length; i++) s[i] *= k;
    }

    static void SalvarWav(string path, float[] s)
    {
        using (var w = new BinaryWriter(File.Create(path)))
        {
            int bytes = s.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes);
            w.Write(new[] { 'W', 'A', 'V', 'E' });
            w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(SR); w.Write(SR * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
            foreach (var v in s) w.Write((short)(Mathf.Clamp(v, -1f, 1f) * 32767f));
        }
    }
}
