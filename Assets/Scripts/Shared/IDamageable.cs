using System.Collections.Generic;
using UnityEngine;

// Tudo que leva dano (jogador, zumbi, Lia)
public interface IDamageable
{
    void ReceberDano(float dano);
    bool EstaVivo { get; }
}

// Quem acompanha o Marcos e os zumbis também atacam (a Lia na noite final)
public interface IAliado : IDamageable
{
    Transform transform { get; }
}

public static class Aliados
{
    // cada aliado entra quando passa a acompanhar o Marcos e sai quando morre/desliga
    public static readonly List<IAliado> Lista = new List<IAliado>();

    public static void Entrar(IAliado a)
    {
        if (!Lista.Contains(a)) Lista.Add(a);
    }

    public static void Sair(IAliado a) => Lista.Remove(a);
}
