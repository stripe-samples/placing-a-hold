<?php
use Slim\Http\Request;
use Slim\Http\Response;
use Stripe\Stripe;

require 'vendor/autoload.php';

$dotenv = Dotenv\Dotenv::create(__DIR__);
$dotenv->load();

require './config.php';

if (PHP_SAPI == 'cli-server') {
  $_SERVER['SCRIPT_NAME'] = '/index.php';
}

$app = new \Slim\App;

// Instantiate the logger as a dependency
$container = $app->getContainer();
$container['logger'] = function ($c) {
  $settings = $c->get('settings')['logger'];
  $logger = new Monolog\Logger($settings['name']);
  $logger->pushProcessor(new Monolog\Processor\UidProcessor());
  $logger->pushHandler(new Monolog\Handler\StreamHandler(__DIR__ . '/logs/app.log', \Monolog\Logger::DEBUG));
  return $logger;
};

$app->add(function ($request, $response, $next) {
    Stripe::setApiKey(getenv('STRIPE_SECRET_KEY'));
    return $next($request, $response);
});


$app->get('/', function (Request $request, Response $response, array $args) {   
  // Display checkout page
  return $response->write(file_get_contents(getenv('STATIC_DIR') . '/index.html'));
});

function calculateOrderAmount($items)
{
  // Replace this constant with a calculation of the order's amount
  // You should always calculate the order total on the server to prevent
  // people from directly manipulating the amount on the client
  return 1400;
}

function generateResponse($intent, $logger) 
{
  switch($intent->status) {
    case "requires_action":
    case "requires_source_action":
      // Card requires authentication
      return [
        'requiresAction'=> true,
        'paymentIntentId'=> $intent->id,
        'clientSecret'=> $intent->client_secret
      ];
    case "requires_payment_method":
    case "requires_source":
      // Card was not properly authenticated, suggest a new payment method
      return [
        error => "Your card was denied, please provide a new payment method"
      ];
    case "succeeded":
      // Payment is complete, authentication not required
      // To cancel the payment after capture you will need to issue a Refund (https://stripe.com/docs/api/refunds)
      $logger->info("💰 Payment received!");
      return ['clientSecret' => $intent->client_secret];
  }
}

$app->get('/stripe-key', function (Request $request, Response $response, array $args) {
    $pubKey = getenv('STRIPE_PUBLISHABLE_KEY');
    return $response->withJson(['publicKey' => $pubKey]);
});


$app->post('/pay', function(Request $request, Response $response) use ($app)  {
  $logger = $this->get('logger');
  $body = json_decode($request->getBody());

  if($body->paymentIntentId == null) {
    // Create new PaymentIntent
    $intent = \Stripe\PaymentIntent::create([
      "amount" => calculateOrderAmount($body->items),
      "currency" => $body->currency,
      "payment_method" => $body->paymentMethodId,
      "confirmation_method" => "manual",
      "capture_method" => "manual",
      "confirm" => true
    ]);
  } else {
    // Confirm the PaymentIntent to collect the money
    $intent = \Stripe\PaymentIntent::retrieve($body->paymentIntentId);
    $intent->confirm();
  }

  if($intent->status == 'requires_capture') {
    $logger->info("❗ Charging the card for: " . $intent->amount_capturable);
    // Because capture_method was set to manual we need to manually capture in order to move the funds
    // You have 7 days to capture a confirmed PaymentIntent
    // To cancel a payment before capturing use .cancel() (https://stripe.com/docs/api/payment_intents/cancel)
    $intent->capture();
  }

  $responseBody = generateResponse($intent, $logger);
  return $response->withJson($responseBody);

});

$app->run();
