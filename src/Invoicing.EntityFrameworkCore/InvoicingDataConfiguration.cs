using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Invoicing.EntityFrameworkCore.Services;

namespace Invoicing.EntityFrameworkCore;

public static class InvoicingDataConfiguration
{
    /// <summary>
    /// Registra los servicios de Invoicing sobre el DbContext <typeparamref name="TContext"/>, que tiene que estar
    /// registrado con <c>AddDbContextFactory</c>. La aplicación registra además <see cref="IInvoiceIssuerProvider"/>
    /// (obligatorio) y, si quiere, <see cref="IInvoiceNumberingLock"/> y <see cref="IReservedSeriesCodes"/>
    /// (antes o después de esta llamada).
    /// </summary>
    public static IServiceCollection AddInvoicingData<TContext>(this IServiceCollection services)
        where TContext : DbContext, IInvoicingDbContext
    {
        services.AddScoped<ICustomerService, CustomerService<TContext>>();
        services.AddScoped<ICustomerInvoiceService, CustomerInvoiceService<TContext>>();
        services.AddScoped<ICustomerInvoiceSeriesService, CustomerInvoiceSeriesService<TContext>>();
        services.AddScoped<IInvoiceDesignService, InvoiceDesignService<TContext>>();
        services.TryAddSingleton<IInvoiceNumberingLock, NoInvoiceNumberingLock>();
        services.TryAddSingleton<IReservedSeriesCodes, NoReservedSeriesCodes>();
        return services;
    }
}
