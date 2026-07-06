namespace RotasEntregaSMA.Dominio.Modelos;

/// <summary>
/// Representa uma coordenada em uma grade urbana simulada (em quilometros a partir de uma origem 0,0).
/// </summary>
public readonly record struct PosicaoGeografica(double X, double Y)
{
    /// <summary>
    /// Distancia euclidiana entre duas posicoes, em quilometros.
    /// </summary>
    public double DistanciaAte(PosicaoGeografica outra)
    {
        var dx = X - outra.X;
        var dy = Y - outra.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public override string ToString() => $"({X:F2}, {Y:F2})";
}
