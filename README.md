# Placing a hold on a card
Charging a card consists of three steps:

**🕵️ Authentication -** Card information is sent to the card issuer for verification. Some cards may require the cardholder to strongly authenticate the purchase through protocols like [3D Secure](https://stripe.com/ie/guides/3d-secure-2). 

**💁 Authorization -** Funds from the customer's account are put on hold but not transferred to the merchant. 

**💸 Capture -** Funds are transferred to the merchant's account and the payment is complete.

The [Payment Intents API](https://stripe.com/docs/api/payment_intents) abstracts away these three stages by handling all steps of the process through the [confirm method](https://stripe.com/docs/api/payment_intents/confirm). If you want to split the authorization + capture steps to place a hold on a customer's card and capture later after a certain event, set capture_method to manual when creating a PaymentIntent.

Note that funds must be captured within **7 days** of authorizing the card or the PaymentIntent reverts back to a status of "requires_payment_method". If you want to charge a customer more than 7 days after collecting their card details see our sample on [saving cards](https://github.com/stripe-samples/saving-card-without-payment).

**Demo**

See a [hosted version](https://nbzjj.sse.codesandbox.io/) of the demo or fork a copy on [codesandbox.io](https://codesandbox.io/s/stripe-sample-placing-a-hold-nbzjj)

The demo is running in test mode -- use `4242424242424242` as a test card number with any CVC code + a future expiration date.

Use the `4000000000003220` test card number to trigger a 3D Secure challenge flow.

Read more about testing on Stripe at https://stripe.com/docs/testing.

<img src="./placing-hold-preview.png" alt="Checkout page to place a hold" align="center">

There are two implementations depending on whether you want to use webhooks for any post-payment process: 
* **[/using-webhooks](/using-webhooks)** Confirms the payment on the client and requires using webhooks or other async event handlers for any post-payment logic (e.g. sending email receipts, fulfilling orders). 
* **[/without-webhooks](/without-webhooks)** Confirms the payment on the server and allows you to run any post-payment logic right after.

This sample shows:
<!-- prettier-ignore -->
|     | Using webhooks | Without webhooks
:--- | :---: | :---:
💳 **Collecting card and cardholder details.** Both integrations use [Stripe Elements](https://stripe.com/docs/stripe-js) to build a custom checkout form. | ✅  | ✅ |
🙅 **Handling card authentication requests and declines.** Attempts to charge a card can fail if the bank declines the purchase or requests additional authentication.  | ✅  | ✅ |
💁 **Placing a hold on a card.** By setting confirmation_method to "manual" when creating a PaymentIntent, you split the authorization and capture steps. | ✅ | ✅ |
↪️ **Using webhooks to respond to a hold being placed on the card.** Confirming the payment on the client requires using webhooks for any follow up actions, like capturing the funds. | ✅ | ❌ |
🏦 **Easily scalable to other payment methods.** Webhooks enable easy adoption of other asynchroneous payment methods like direct debits and push-based payment flows. | ✅ | ❌ |


## How to run locally
This sample includes 5 server implementations in Node, Ruby, Python, Java, and PHP for the two integration types: [using-webhooks](/using-webhooks) and [without-webhooks](/without-webhooks). 

If you want to run the sample locally copy the .env.example file to your own .env file: 

```
cp .env.example .env
```

Then follow the instructions in the server directory to run.

You will need a Stripe account with its own set of [API keys](https://stripe.com/docs/development#api-keys).


## FAQ
Q: Why did you pick these frameworks?

A: We chose the most minimal framework to convey the key Stripe calls and concepts you need to understand. These demos are meant as an educational tool that helps you roadmap how to integrate Stripe within your own system independent of the framework.

Q: Can you show me how to build X?

A: We are always looking for new sample ideas, please email dev-samples@stripe.com with your suggestion!

## Author(s)
[@adreyfus-stripe](https://twitter.com/adrind)
