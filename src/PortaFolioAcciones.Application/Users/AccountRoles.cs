namespace PortaFolioAcciones.Application.Users;

/// <summary>
/// Nombres de los papeles.
///
/// Viven en la capa de aplicacion y no en la de presentacion porque las dos los necesitan: la API para
/// exigir el papel en los endpoints, y la infraestructura para asignarlo al crear una cuenta. Ponerlos
/// en un solo sitio evita que se escriban de dos formas distintas y dejen de coincidir.
/// </summary>
public static class AccountRoles
{
    /// <summary>Puede ver y modificar los datos de todas las personas, con auditoria.</summary>
    public const string Administrator = "Administrator";
}
