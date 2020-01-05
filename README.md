# Websocketserver and Client
## Usage 
* Start the server via visual studio
* Open the HTML client in the browser
* The client should connect to the server
* Send the animationtype to the server via HTML input

## Animationtypes:
Send following String to server to start animation
* yoda
* babyyoda
* earth
## Closing Connection
* Send "exit" as input 
* Close the client in browser

## Tracing

The Websocketserver is instrumentalized. 

To view the reported trace and spans use the Jaeger binary:
https://www.jaegertracing.io/download/
### Example Usage 
This Example uses the jaeger-all-in-one executable

```
./jaeger-all-in-one.exe --collector.zipkin.http-port=9411
``` 
