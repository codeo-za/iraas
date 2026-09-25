# Image Resizing As A Service

## What is it
A (very thin) web wrapper around [ImageSharp](https://github.com/SixLabors/ImageSharp) to provide
image resizing as a web service.

## Why?
The original intent was to improve the customer experience in
a web-based ordering system by decreasing download time and
providing images that are correctly scaled for the current
display dimensions.

In this way, mobile-vs-desktop and different
generations of mobile devices with different device pixel ratios
and resolutions can be served by a web component which calculates
the correct size for the image and then renders that image instead
of the original.

With this in mind, it should be obvious why IRAAS won't scale images
*up* as there's no benefit to the customer - the web container will
already scale the image up if it's too small.

In addition to resizing, IRAAS can transcode images to different
formats with a number of options, all thanks to 
[SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp).

If you ensure that `EnableTestPage` is set to `true`, then you should
find a page at /test where you can enter an image url and tweak
the parameters sent to the resize service. This can be useful for
testing what defaults to use and ensuring that domain whitelists
are appropriately set.

## Requirements
- Node (for build)
- dotnet sdk 7.0 or better

## Configuration
IRAAS has a few configuration options, explained below:

Option                          | Default                      | Notes
--------------------------------|------------------------------|------
MaxInputImageSize               | 41943040 (40mb)              | The maximum size, in bytes, that will be accepted for resizing an image, preventing DDoS via gigantic images.
MaxOutputImageSize              | 41943040 (40mb)              | The maximum output size, in bytes, to allow. Outputs which are larger than this are discarded
UseDeveloperExceptionPage       | false                        | Flag: when enabled, if IRAAS encounters an error, it will show the developer exception page instead of the default short response
UseHttps                        | false                        | Flag: when enabled, requests coming in on http will be redirected to the https url you configure in `Urls`
EnableTestPage                  | false                        | Flag: when enabled, the path `/test` will serve a (very basic) web page to test resizing images
DomainWhitelist                 | *                            | Comma-delimited list of domains or domain globs which are allowed as image sources, eg `*.mydomain.com`. Setting this to "*" allows all domains.
MaxConcurrency                  | 0                            | The maximum number of image resize operations to perform concurrently. When set to 0, will default to using the number of processors on the host
MaxImageFetchTimeInMilliseconds | 10000 (10s)                  | The maximum time to wait for a remote service to provide a requested image, in milliseconds
MaxClients                      | 0                            | The maximum number of requests to service at one time. Image resize requests are queued in order, and results for exactly the same parameters are re-used, so requests will queue once MaxConcurrency is reached and images will be handled when resources are available. When set to 0, this instructs IRAAS not to throttle incoming requests, but to handle them as and when it can
ShareConcurrentRequests         | true                         | Flag: when set to true, if two concurrent requests for the same image with the same parameters are received, only one resize operation is done and the result is copied to the other response
EnableConnectionKeepAlive       | false                        | Flag: when set to true, connections to remote image servers are kept open with http keep-alive
SuppressErrorDiagnostics        | false                        | Flag: when set to true, error responses will be secretive about what happened, so as not to leak details to an attacker
MaxUrlFetchRetries              | 1                            | The number of times to _retry_ a failed image fetch request. If set to zero, only the initial request is performed. Setting this to a low number like 1 or 2 may work around transient errors from remote image servers
AllowInvalidSSLCertificates     | false                        | Flag: when set to true, invalid ssl certificates are ignored when fetching images to resize (useful for dev-testing against self-signed certificates)
AllowPostRequests               | false                        | Flag: when set to true, clients may post raw image data and resize parameters to IRAAS and receive the resize result, if they pass other limitations
PostAuthTokens                  | ""                           | A comma-separated list of allowed tokens for Bearer authentication when POST requests are allowed. When it's empty, no POST request will be allowed. You may allow all POST requests by setting this to '*'.

## Operation notes
1. Remember: garbage in, garbage out - IRAAS can't make your images prettier and won't resize them larger
2. It is suggested to disable the test page in production
3. IRAAS only generates two http status codes: 500 (something seriously went wrong) and 503 (temporarily unavailable - 
    MaxClients has been reached). Other status codes come from the upstream server, so if that returns a 201, then that's
    what the client will see. If you see any whacky status codes, first look upstream.
4. IRAAS will echo any provided request headers to the remote image service, and echo any response headers from that
    remote image service back to the client. So if you have custom headers on your image responses, they will get back
    to the client
5. POST requests are disabled by default, but may be enabled via AllowPostRequests - however, you will also
   need to set PostAuthTokens: when this setting is empty, all POST requests are still denied. PostAuthTokens
   is a comma-separated list of tokens which are allowed in a client's Authorization header (using the Bearer scheme).
   Examples, with AllowPostRequests set to "true":
   - config has "PostAuthTokens": "" -> all POST requests will fail, even though AllowPostRequests is set true
   - config has "PostAuthTokens": "token1,token2,token3" -> a client with the header "Authorization: Bearer token2" will
     be allowed to proceed. It's suggested to use something like a GUID for tokens.
   - config has "PostAuthTokens": "*" -> any POST request is allowed, even if it doesn't have an Authorization header
   - MaxInputImageSize still applies, but is bounded by the kestrel limits (if set), IIS, or any
     reverse-proxy upstream. If this is not set in kestrel config, then it is determined from the MaxInputImageSize
     when IRAAS starts up, allowing 50% headroom for base64 encoded inputs, and any resize parameters involved. 
     Base64-encoded binary data typically bloats by around 33%, so the extra 17% allows some headroom in which a slightly
     oversized request will 400 (warning about hitting limits). Severely oversized requests will receive
     a 413 response from Kestrel (or whatever response IIS or upstream reverse-proxies provide).
   _NB: It is highly recommended to only allow POST requests over SSL (via your own upstream reverse-proxy) or on a
    secure network, otherwise all tokens will be visible to anything between the client and IRAAS._

If you're configuring via environment variables and would like to verify that they are being set correctly,
set the environment variable DUMP_ENVIRONMENT to "1" before starting the server and the full environment
available to the server will be dumped out during startup.

## Running direct-from-code
```
npm install
npm start
```

You should see the service listening on `http://127.0.0.1:5000`

## Containerisation
see [release.yml](.github/workflows/release.yml) for a starter - the GitHub
Action starts with producing a docker container for IRAAS and pushing to a
registry. Fork and add your own logic for deploying from the registry
to your container host.

## Publishing to Octopus
```
npm i
npm run pack
```
This will produce a nuget package ready for octopus deployment in the `packages` folder