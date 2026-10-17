using System.Security.Cryptography;

namespace PortaFolioAcciones.Infrastructure.Identity;

/// <summary>
/// Genera contrasenas largas y aleatorias.
///
/// Se usan <b>letras y numeros, sin simbolos</b>, y no por comodidad: estas contrasenas hay que
/// entregarlas <b>en mano</b> o dictarlas, y un simbolo que se confunde al copiarlo solo consigue que
/// alguien no pueda entrar y haya que repetir el proceso.
///
/// Se excluyen las letras que se confunden entre si (<c>l</c>, <c>I</c>, <c>1</c>, <c>O</c>, <c>0</c>) por
/// el mismo motivo.
/// </summary>
public static class PasswordGenerator
{
    private const string Alfabeto = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private const int Longitud = 20;

    /// <summary>Genera una contrasena que cumple la politica de la aplicacion.</summary>
    public static string Generate()
    {
        var caracteres = new char[Longitud];

        for (var i = 0; i < caracteres.Length; i++)
        {
            caracteres[i] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
        }

        // Se fuerza el principio para que cumpla la politica pase lo que pase, y no dependa de que el
        // generador haya tenido la suerte de incluir una mayuscula y un numero.
        return $"Aa1{new string(caracteres)}";
    }
}
