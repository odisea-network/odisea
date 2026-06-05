using Microsoft.EntityFrameworkCore;
using Odisea.Domain.Entities;

namespace Odisea.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Offer> Offers { get; }
    DbSet<Collection> Collections { get; }
    DbSet<Agency> Agencies { get; }
    DbSet<Operator> Operators { get; }
    DbSet<Theme> Themes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
