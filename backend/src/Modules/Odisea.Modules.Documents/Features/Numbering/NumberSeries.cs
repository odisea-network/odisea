using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Documents.Infrastructure;

namespace Odisea.Modules.Documents.Features.Numbering;

public class NumberSeries(DocumentsDbContext db)
{
    public const string TaxDocuments = "tax";

    /// Atomic, gapless increment. MUST be called inside the transaction that
    /// also inserts the document: a failed insert then rolls the number back,
    /// keeping the series hole-free (чл.78 ППЗДДС).
    public async Task<string> NextAsync(string series, CancellationToken ct)
    {
        long next;
        if (db.Database.IsRelational())
        {
            // Single atomic upsert; the row lock serializes concurrent issuers.
            var rows = await db.Database.SqlQuery<long>(
                $@"INSERT INTO documents.document_counters AS c (series, last_number)
                   VALUES ({series}, 1)
                   ON CONFLICT (series) DO UPDATE SET last_number = c.last_number + 1
                   RETURNING last_number AS ""Value""")
                .ToListAsync(ct);
            next = rows[0];
        }
        else
        {
            // InMemory fallback for unit tests.
            var counter = await db.DocumentCounters.FindAsync([series], ct);
            if (counter is null)
            {
                counter = new Domain.DocumentCounter { Series = series, LastNumber = 0 };
                db.DocumentCounters.Add(counter);
            }
            counter.LastNumber++;
            await db.SaveChangesAsync(ct);
            next = counter.LastNumber;
        }

        return next.ToString("D10");
    }
}
