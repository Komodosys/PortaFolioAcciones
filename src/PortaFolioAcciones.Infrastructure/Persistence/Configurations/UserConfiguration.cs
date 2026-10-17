using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortaFolioAcciones.Domain.Entities;

namespace PortaFolioAcciones.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de la persona.
///
/// Aqui <b>no hay contrasena</b>: las credenciales las guarda ASP.NET Core Identity en su propia tabla,
/// <c>AspNetUsers</c>, y la relacion entre las dos es que comparten identificador. Por eso esta tabla no
/// tiene ninguna columna de autenticacion.
/// </summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Se llama UserProfiles y no Users a proposito: Identity tiene su propia tabla, AspNetUsers,
        // con las credenciales. Si esta se llamara Users, quien inspeccione la base de datos veria dos
        // tablas de usuarios sin saber cual es cual. Asi se lee solo.
        builder.ToTable("UserProfiles");
        builder.HasKey(user => user.Id);

        builder.Property(user => user.DisplayName)
            .HasMaxLength(User.MaxDisplayNameLength)
            .IsRequired();

        builder.Property(user => user.Email).HasMaxLength(ColumnPrecision.Name);

        builder.Property(user => user.BaseCurrency)
            .HasMaxLength(ColumnPrecision.CurrencyCode)
            .HasConversion(ValueObjectMapping.CurrencyConverter)
            .IsRequired();

        builder.Property(user => user.PreferredLanguage)
            .HasMaxLength(2)
            .HasConversion(ValueObjectMapping.LanguageConverter)
            .IsRequired();

        builder.Property(user => user.CreatedAtUtc).IsRequired();
        builder.Property(user => user.UpdatedAtUtc).IsRequired();

        // El correo identifica la cuenta, asi que no puede repetirse. Se permite nulo porque se puede
        // crear una persona sin correo: no hay servidor de correo y no hace falta para nada.
        builder.HasIndex(user => user.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");
    }
}
