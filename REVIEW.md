# Code review (code review and optional LLM design)

## 1. Orders controller

### Findings, ordered by severity

1. **Critical — SQL injection.** Concatenating `customer` into SQL allows an attacker to change the query, access or alter data. Parameterize the value; do not interpolate it into SQL.
2. **Critical — exposed privileged credentials.** A production `sa` password is in source code. Treat it as compromised, rotate it and audit use. Use a secret store and a least-privilege database identity.
3. **High — false success and lost failures.** The POST catches every exception and returns 200 even if nothing was saved. Clients cannot retry safely or know their order was lost. Log unexpected failures centrally and return a 5xx ProblemDetails response; return 201 and a resource location on success.
4. **High — missing authorization.** No access checks are visible on endpoints that read and create orders. Apply authentication and appropriate policies and scope reads to the caller's permitted customers. The sample does not establish whether protection exists elsewhere, so verify the global policy.
5. **High — overposting and data exposure.** Binding and returning persistence entities allows callers to set unintended fields and exposes the database model. Use validated request/response DTOs and explicitly map permitted fields.
6. **High — no input validation.** A null, blank or excessively long customer and invalid order data are not rejected. Use bounded DTO validation and meaningful 400 responses; define business rules before accepting an order.
7. **High — resources leak.** Connections, commands and readers are not disposed; a new HttpClient is created for every order and never disposed. This can exhaust connection pools and sockets. Use scoped DI for EF and `IHttpClientFactory` for HTTP.
8. **High — synchronous I/O and blocking on async.** Database I/O and `.Result` block request threads, reducing throughput and potentially causing deadlock in some synchronization contexts. Use asynchronous methods throughout and propagate cancellation.
9. **High — unbounded N+1 external calls.** The query has no page limit and calls the tax service once per order sequentially. Large results are slow and expensive and could overwhelm the service. Page results, use a batch API if available or bounded concurrency; define timeout, retry and failure behavior. Blind retries can amplify an outage.
10. **Medium — parsing and remote failures.** Tax responses can be malformed, culture-dependent, unavailable or slow; `decimal.Parse` and `.Result` can fail the whole request. Validate invariant-culture values, configure timeouts and log failures without leaking details. Agree whether partial responses are acceptable.
11. **Medium — direct DbContext construction.** It bypasses configured options, lifecycle, testing and connection management. Inject the context with request scope.
12. **Medium — `SELECT *` and fragile mapping.** Extra columns consume bandwidth, schema changes can break assumptions, and unchecked casts fail on null or type mismatches. Select and map explicit columns; configure nullability.
13. **Medium — inconsistent access paths and hard-coded endpoint.** Raw ADO.NET and a separately constructed EF context can target different databases, while the tax URL is environment-specific. Use injected configuration and a single consistent data access approach.
14. **Low — missing pagination, deterministic ordering and observability.** An unbounded result and no ordering make clients slow and paging impossible. Add stable ordering and bounded pagination, structured logging and metrics. Avoid logging names or secrets.

### Illustrative rewrite

Assumptions: `Order` has `Id`, `CustomerName`, `Total` and `Tax`; the authenticated policy enforces the application's customer access rules; tax values are plain invariant decimal strings. Replace the authorization placeholder with actual tenant scoping and register `ShopDbContext`, authorization, and a typed HttpClient with an appropriate base address and timeout. An exception handler must map unexpected exceptions to generic ProblemDetails without leaking internals. The code chooses to fail the request when tax is unavailable, rather than silently return an incorrect value.

```csharp
[ApiController]
[Route("api/orders")]
[Authorize(Policy = "OrdersAccess")]
public sealed class OrdersController(ShopDbContext db, TaxClient tax) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<OrderResponse>>> Search(
        [FromQuery, Required, StringLength(120, MinimumLength = 1)] string customer,
        [FromQuery, Range(1, 10000)] int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customer))
            return ValidationProblem(new Dictionary<string, string[]> { ["customer"] = ["Customer is required."] });
        // Add tenant/customer access predicate according to the authorization model.
        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.CustomerName == customer.Trim()).OrderBy(o => o.Id)
            .Skip((page - 1) * 50).Take(50)
            .Select(o => new { o.Id, o.Total }).ToListAsync(cancellationToken);
        var results = new List<OrderResponse>();
        foreach (var order in orders)
        {
            var rate = await tax.GetTaxAsync(order.Id, cancellationToken);
            results.Add(new OrderResponse(order.Id, order.Total, rate));
        }
        return Ok(results);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var order = new Order { CustomerName = request.CustomerName.Trim(), Total = request.Total };
        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/orders/{order.Id}", new { order.Id });
    }
}
public record OrderResponse(int Id, decimal Total, decimal Tax);
public sealed class CreateOrderRequest : IValidatableObject
{
    [Required, StringLength(120)] public string CustomerName { get; init; } = "";
    [Range(typeof(decimal), "0.01", "1000000")] public decimal Total { get; init; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(CustomerName))
            yield return new ValidationResult("Customer is required.", [nameof(CustomerName)]);
    }
}
public sealed class TaxClient(HttpClient http)
{
    public async Task<decimal> GetTaxAsync(int orderId, CancellationToken ct)
    {
        var text = await http.GetStringAsync($"tax/{orderId}", ct);
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value < 0)
            throw new InvalidOperationException("Tax service returned an invalid value.");
        return value;
    }
}
```

The returned location requires a corresponding authorized GET endpoint in the complete API. For real throughput, replace the illustrative sequential tax loop with a batch endpoint or carefully bounded parallel calls; do not use the EF context concurrently.

## 2. SQL Server report

### Findings, ordered by severity

1. **High — `NOT IN` and NULL.** If any `Refunds.OrderId` is NULL, `NOT IN` evaluates to unknown for otherwise valid rows and can return no orders. Use `NOT EXISTS` and index its correlation key.
2. **High — non-sargable date predicate.** `YEAR(o.OrderDate)` applies a function to the column and prevents a normal range seek. Use an inclusive start and exclusive next-year boundary, which also handles time components correctly.
3. **High — leading wildcard search.** `LIKE '%...%'` generally cannot use an ordinary B-tree index to seek by name. An index may still cover a scan; evaluate SQL Server full-text search if word/prefix semantics are acceptable, or a purpose-built substring index/search service if exact substring behavior is essential.
4. **Medium — unbounded result and sort.** Returning every matching order can consume memory, network bandwidth and tempdb. Add deterministic pagination or stream/export asynchronously as required; preserve the report's all-orders semantics if pagination is not allowed.
5. **Medium — `SELECT *`.** It reads and exposes unnecessary columns, increases I/O and makes the output fragile when schemas change. Select only required columns, explicitly aliasing duplicates.
6. **Medium — implicit join syntax.** The comma join obscures relationships and makes accidental Cartesian joins easier. Use explicit `INNER JOIN`.
7. **Medium — ambiguous name and pattern behavior.** `LIKE` treats `%`, `_` and `[` in the user input as patterns, and collation controls case/accent behavior. Escape wildcard characters if the intended behavior is literal substring search; define NULL/empty search behavior.
8. **Low — nondeterministic order on ties.** Equal `OrderDate` values have no stable order. Add `Id` as a tie-breaker, especially when paging.

### Rewrite and indexes

```sql
-- @pattern is a parameter constructed by escaping ~, %, _, and [ in the
-- literal user term with ~, then adding % at both ends. Reject NULL if required.
SELECT o.Id, o.OrderDate, o.CustomerId, o.Total, c.Name AS CustomerName
FROM dbo.Orders AS o
INNER JOIN dbo.Customers AS c ON c.Id = o.CustomerId
WHERE o.OrderDate >= '20250101' AND o.OrderDate < '20260101'
  AND c.Name LIKE @pattern ESCAPE '~'
  AND NOT EXISTS (SELECT 1 FROM dbo.Refunds AS r WHERE r.OrderId = o.Id)
ORDER BY o.OrderDate DESC, o.Id DESC;
```

Consider `Orders(OrderDate DESC, Id DESC) INCLUDE (CustomerId, Total)` to support the date range and ordering, `Refunds(OrderId)` for the anti-join, and `Customers(Id) INCLUDE (Name)` if the existing key does not cover the lookup. Depending on selectivity and the actual plan, `Orders(CustomerId, OrderDate DESC, Id DESC) INCLUDE (Total)` may be better when matching customers are identified first. Do not create all indexes blindly: inspect actual execution plans, row counts and logical reads on representative data, balancing write and storage cost. A normal `Name` index cannot seek arbitrary substrings.

## Bonus: LLM design

- Queue a suggestion job on the server after creating a ticket; return immediately.
- Send only the minimum redacted ticket text to an approved provider over TLS.
- Keep the API key in a secret manager, never in the browser or repository.
- Use a fixed system instruction and clearly delimit user text as untrusted data.
- Ask for a short summary and one priority from a fixed JSON schema.
- Validate the response and treat suggestions as advisory, never automatic authority.
- Set timeouts, bounded retries with backoff, and an explicit failed/pending state.
- Limit input/output tokens, rate-limit requests and monitor usage and cost.
- Let an agent review or override the result; avoid logging sensitive content.
- Define retention, consent, vendor data-use settings and access controls before rollout.
