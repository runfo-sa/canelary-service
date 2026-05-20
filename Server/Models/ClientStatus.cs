using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models
{
    /// <summary>
    /// Representación del estado de un cliente en una tabla.
    /// </summary>
    [PrimaryKey(nameof(Id))]
    [Index(nameof(Cliente), IsUnique = true)]
    public class ClientStatus
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column(Order = 1)]
        public int Id { get; set; }

        public string Cliente { get; set; }
        public Status Estado { get; set; }
        public DateTime UltimaConexion { get; set; }

        public List<EtiquetaCliente> Etiquetas { get; set; } = new();

        public ClientStatus(string Cliente, Status Estado, DateTime UltimaConexion)
        {
            this.Cliente = Cliente;
            this.Estado = Estado;
            this.UltimaConexion = UltimaConexion;
        }

        public ClientStatus(string Cliente, Status Estado)
        {
            this.Cliente = Cliente;
            this.Estado = Estado;
            UltimaConexion = DateTime.Now;
        }

        public override string ToString()
        {
            return Cliente + "::" + Estado + "::" + UltimaConexion;
        }
    }

    public enum TipoDiff : byte
    {
        Desactualizada = 0,
        Sobrante = 1
    }

    /// <summary>
    /// Etiqueta que difiere entre cliente y servidor: faltante (Desactualizada) o no esperada (Sobrante).
    /// </summary>
    [PrimaryKey(nameof(Id))]
    public class EtiquetaCliente
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int EstadoClienteId { get; set; }
        public string Nombre { get; set; }
        public TipoDiff Tipo { get; set; }

        public EtiquetaCliente(string Nombre, TipoDiff Tipo)
        {
            this.Nombre = Nombre;
            this.Tipo = Tipo;
        }

        public EtiquetaCliente(int EstadoClienteId, string Nombre, TipoDiff Tipo)
        {
            this.EstadoClienteId = EstadoClienteId;
            this.Nombre = Nombre;
            this.Tipo = Tipo;
        }
    }

    /// <summary>
    /// Instancia de conexión con una base de datos para la tabla de <see cref="ClientStatus"/>
    /// </summary>
    public class ClientStatusDb(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ClientStatus> EstadoCliente { get; set; } = null!;
        public DbSet<EtiquetaCliente> EtiquetasCliente { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("service");

            modelBuilder.Entity<ClientStatus>(e =>
            {
                e.ToTable(b => b.IsMemoryOptimized());
                e.HasMany(c => c.Etiquetas)
                    .WithOne()
                    .HasForeignKey(et => et.EstadoClienteId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<EtiquetaCliente>(e =>
            {
                e.ToTable("EtiquetaCliente", b => b.IsMemoryOptimized());
                e.HasIndex(et => et.EstadoClienteId);
            });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();
            optionsBuilder.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
        }

        public ClientStatus? Find(string Name)
        {
            List<ClientStatus> clientStatus = [.. EstadoCliente.Where(e => e.Cliente == Name)];
            return clientStatus.IsNullOrEmpty() ? null : clientStatus.First();
        }
    }
}
