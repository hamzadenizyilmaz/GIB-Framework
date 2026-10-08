using GIBFramework.DTOs;
using GIBFramework.Models.Tenancy;

namespace GIBFramework.Mapper;

public static class InvoiceMapper
{
    public static Invoice ToDraft(this CreateInvoiceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new Invoice
        {
            DeliveryDate = request.DeliveryDate,
            Profile = request.Profile,
            TypeCode = request.TypeCode,
            Currency = request.Currency.ToUpperInvariant(),
            ExchangeRate = request.ExchangeRate,
            Customer = request.Customer.ToParty(),
            Lines = [.. request.Lines.Select(l => new InvoiceLine
            {
                Name = l.Name,
                Description = l.Description,
                Quantity = l.Quantity,
                UnitCode = l.UnitCode,
                UnitPrice = l.UnitPrice,
                DiscountAmount = l.DiscountAmount,
                VatRate = l.VatRate,
                VatExemptionCode = string.IsNullOrWhiteSpace(l.VatExemptionCode) ? null : l.VatExemptionCode,
                WithholdingCode = string.IsNullOrWhiteSpace(l.WithholdingCode) ? null : l.WithholdingCode,
            })],
            Notes = [.. request.Notes.Where(n => !string.IsNullOrWhiteSpace(n))],
            DespatchRequired = request.DespatchRequired,
            DespatchNumber = request.DespatchNumber,
            DespatchDate = request.DespatchDate,
            OrderNumber = request.OrderNumber,
            SendingType = request.SendingType,
        };
    }

    public static InvoiceParty ToParty(this PartyDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var taxId = string.IsNullOrWhiteSpace(dto.TaxId) ? TaxIdentifier.AnonymousConsumer : dto.TaxId.Trim();
        return new InvoiceParty
        {
            TaxId = taxId,
            Kind = taxId.Length == 11 ? PartyKind.NaturalPerson : dto.Kind,
            Regime = taxId == TaxIdentifier.AnonymousConsumer ? BookkeepingRegime.NotATaxpayer : dto.Regime,
            Title = dto.Title.Trim(),
            FirstName = dto.FirstName,
            FamilyName = dto.FamilyName,
            TaxOffice = dto.TaxOffice,
            Neighborhood = string.IsNullOrWhiteSpace(dto.Neighborhood) ? null : dto.Neighborhood.Trim(),
            Street = dto.Street,
            BuildingNumber = dto.BuildingNumber,
            District = dto.District,
            City = dto.City,
            PostalCode = dto.PostalCode,
            Country = dto.Country,
            Email = dto.Email,
            Phone = dto.Phone,
            Alias = dto.Alias,
        };
    }

    public static Tenant ToTenant(this CreateTenantRequest request, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        var profile = request.Profile.ToParty();
        profile.IsEFaturaRegistered = request.IsEFaturaRegistered;
        profile.IsEArchiveRegistered = request.IsEArchiveRegistered;
        return new Tenant
        {
            Id = Guid.CreateVersion7(now),
            Name = request.Name,
            Profile = profile,
            EFaturaPrefix = request.EFaturaPrefix,
            EArsivPrefix = request.EArsivPrefix,
            IsActive = true,
            CreatedAt = now,
        };
    }

    public static InvoiceView ToView(this Invoice invoice, bool maskPersonalData)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        var d = invoice.Decision;
        return new InvoiceView
        {
            Id = invoice.Id,
            Ettn = invoice.Uuid,
            DocumentNumber = invoice.DocumentNumber,
            DraftNumber = invoice.DraftNumber,
            Status = invoice.Status,
            AllowedTransitions = DocumentLifecycle.AllowedFrom(invoice.Status),
            DocumentType = invoice.DocumentType,
            Profile = invoice.Profile,
            TypeCode = invoice.TypeCode,
            IssueDate = invoice.IssueDate,
            IssueTime = invoice.IssueTime,
            DeliveryDate = invoice.DeliveryDate,
            Currency = invoice.Currency,
            ExchangeRate = invoice.ExchangeRate,
            Supplier = invoice.Supplier,
            Customer = maskPersonalData ? Mask(invoice.Customer) : invoice.Customer,
            Lines = invoice.Lines,
            Notes = invoice.Notes,
            Totals = invoice.Totals,
            Decision = d is null ? null : new DecisionView(d.DecisionId, d.DocumentType, d.Validation, d.RoutingRuleVersion, d.LegalBasis, d.Explanation, d.Findings, d.UsesUnapprovedRules, d.RuleSetHash),
            LastError = invoice.LastError,
            ProviderReference = invoice.ProviderReference,
            CreatedBy = invoice.CreatedBy,
            CreatedAt = invoice.CreatedAt,
            ApprovedBy = invoice.ApprovedBy,
            SignedBy = invoice.SignedBy,
            SignedAt = invoice.SignedAt,
            SignedXmlSha256 = invoice.SignedXmlSha256,
            IssuanceChannel = invoice.IssuanceChannel,
            PortalEnvironment = invoice.PortalEnvironment,
            DespatchNumber = invoice.DespatchNumber,
            OrderNumber = invoice.OrderNumber,
            UpdatedAt = invoice.UpdatedAt,
        };
    }

    private static InvoiceParty Mask(InvoiceParty party)
    {
        if (party.TaxId.Length != 11 || !TaxIdentifier.TryParse(party.TaxId, out var id, out _))
        {
            return party;
        }

        var masked = JsonDefaults.Deserialize<InvoiceParty>(JsonDefaults.Serialize(party));
        masked.TaxId = id.Masked();
        masked.Email = masked.Email is null ? null : "***";
        masked.Phone = masked.Phone is null ? null : "***";
        return masked;
    }
}
