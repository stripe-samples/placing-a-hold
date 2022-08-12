using Microsoft.Extensions.Options;
using Stripe;

DotNetEnv.Env.Load();
StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");

StripeConfiguration.AppInfo = new AppInfo
{
    Name = "https://github.com/stripe-samples/charging-a-saved-card",
    Url = "https://github.com/stripe-samples",
    Version = "0.1.0",
};

StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = Environment.GetEnvironmentVariable("STATIC_DIR")
});

builder.Services.Configure<StripeOptions>(options =>
{
    options.PublishableKey = Environment.GetEnvironmentVariable("STRIPE_PUBLISHABLE_KEY");
    options.SecretKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");
    options.WebhookSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET");
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/create-payment-intent", async (HttpRequest request, IOptions<StripeOptions> stripeOptions) =>
{
    var paymentIntentSvc = new PaymentIntentService();
    var piOptions = new PaymentIntentCreateOptions
    {
        Amount = 1400,
        Currency = "usd",
        CaptureMethod = "manual"
    };

    var paymentIntent = await paymentIntentSvc.CreateAsync(piOptions);
    return Results.Ok(new
    {
        publicKey = stripeOptions.Value.PublishableKey,
        clientSecret = paymentIntent.ClientSecret,
        id = paymentIntent.Id
    });
});

app.MapPost("/webhook", async (HttpRequest request, IOptions<StripeOptions> options) =>
{
    var json = await new StreamReader(request.Body).ReadToEndAsync();
    Event stripeEvent;
    try
    {
        stripeEvent = EventUtility.ConstructEvent(
            json,
            request.Headers["Stripe-Signature"],
             options.Value.WebhookSecret
        );
        app.Logger.LogInformation($"Webhook notification with type: {stripeEvent.Type} found for {stripeEvent.Id}");

        var intentData = stripeEvent.Data.Object as PaymentIntent;

        if (stripeEvent.Type == "payment_intent.amount_capturable_updated")
        {
            app.Logger.LogInformation("❗ Charging the card for: {AmountCapturable}", intentData.AmountCapturable);

            // You can capture an amount less than or equal to the amount_capturable
            // By default capture() will capture the full amount_capturable
            //To cancel a payment before capturing use .cancel() (https://stripe.com/docs/api/payment_intents/cancel)
            var paymentIntentSvc = new PaymentIntentService();
            intentData = await paymentIntentSvc.CaptureAsync(intentData.Id);
        }
        else if (stripeEvent.Type == "payment_intent.succeeded")
        {
            app.Logger.LogInformation("💰 Payment received!");
            // Fulfill any orders, e-mail receipts, etc
            // To cancel the payment after capture you will need to issue a Refund (https://stripe.com/docs/api/refunds)
        }
        else if (stripeEvent.Type == "payment_intent.payment_failed")
        {
            app.Logger.LogError("❌ Payment failed.");
        }
    }
    catch (StripeException ex)
    {
        app.Logger.LogError(ex, ex.Message);
        return Results.BadRequest();
    }

    return Results.Ok(new { status = "success" });
});

app.Run();
