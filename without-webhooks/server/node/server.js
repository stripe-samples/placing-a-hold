const express = require("express");
const app = express();
const { resolve } = require("path");
// Replace if using a different env file or config
const env = require("dotenv").config({ path: "./.env" });
const stripe = require("stripe")(process.env.STRIPE_SECRET_KEY);

app.use(express.static(process.env.STATIC_DIR));
app.use(
  express.json({
    // We need the raw body to verify webhook signatures.
    // Let's compute it only when hitting the Stripe webhook endpoint.
    verify: function(req, res, buf) {
      if (req.originalUrl.startsWith("/webhook")) {
        req.rawBody = buf.toString();
      }
    }
  })
);

app.get("/", (req, res) => {
  // Display checkout page
  const path = resolve(process.env.STATIC_DIR + "/index.html");
  res.sendFile(path);
});

app.get("/stripe-key", (req, res) => {
  res.send({ publicKey: process.env.STRIPE_PUBLIC_KEY });
});

const calculateOrderAmount = items => {
  // Replace this constant with a calculation of the order's amount
  // You should always calculate the order total on the server to prevent
  // people from directly manipulating the amount on the client
  return 1400;
};

app.post("/pay", async (req, res) => {
  const { paymentMethodId, paymentIntentId, items, currency } = req.body;

  const orderAmount = calculateOrderAmount(items);

  try {
    let intent;
    if (!paymentIntentId) {
      // Create new PaymentIntent
      intent = await stripe.paymentIntents.create({
        amount: orderAmount,
        currency: currency,
        payment_method: paymentMethodId,
        confirmation_method: "manual",
        capture_method: "manual",
        confirm: true
      });
    } else {
      // Confirm the PaymentIntent to place a hold on the card
      intent = await stripe.paymentIntents.confirm(paymentIntentId);
    }

    if (intent.status === "requires_capture") {
      console.log("❗ Charging the card for: " + intent.amount_capturable);
      // Because capture_method was set to manual we need to manually capture in order to move the funds
      // You have 7 days to capture a confirmed PaymentIntent
      // To cancel a payment before capturing use .cancel() (https://stripe.com/docs/api/payment_intents/cancel)
      intent = await stripe.paymentIntents.capture(intent.id);
    }

    const response = generateResponse(intent);
    res.send(response);
  } catch (e) {
    // Handle "hard declines" e.g. insufficient funds, expired card, etc
    // See https://stripe.com/docs/declines/codes for more
    res.send({ error: e.message });
  }
});

const generateResponse = intent => {
  // Generate a response based on the intent's status
  switch (intent.status) {
    case "requires_action":
    case "requires_source_action":
      // Card requires authentication
      return {
        requiresAction: true,
        paymentIntentId: intent.id,
        clientSecret: intent.client_secret
      };
    case "requires_payment_method":
    case "requires_source":
      // Card was not properly authenticated, suggest a new payment method
      return {
        error: "Your card was denied, please provide a new payment method"
      };
    case "succeeded":
      // Payment is complete, authentication not required
      // To cancel the payment after capture you will need to issue a Refund (https://stripe.com/docs/api/refunds)
      console.log("💰 Payment received!");
      return { clientSecret: intent.client_secret };
  }
};

app.listen(4242, () => console.log(`Node server listening on port ${4242}!`));
