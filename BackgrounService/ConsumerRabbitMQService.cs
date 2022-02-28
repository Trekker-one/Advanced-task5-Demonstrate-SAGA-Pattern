using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using CartService.DBContext;
using CartService.Models;
using Newtonsoft.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace CartService.BackgrounService
{
    public class ConsumerRabbitMQService: BackgroundService
    {
        private readonly ILogger _logger;
        private IConnection _connection;
        private IModel _channel;
        private IModel _orderProcessedChannel;
        private CartDbContext _context;

        public ConsumerRabbitMQService(ILoggerFactory loggerFactory, IServiceScopeFactory factory)
        {
            this._logger = loggerFactory.CreateLogger<ConsumerRabbitMQService>();
            this._context = factory.CreateScope().ServiceProvider.GetRequiredService<CartDbContext>();
            InitRabbitMQ();
        }

        private void InitRabbitMQ()
        {
            var factory = new ConnectionFactory
            {
                HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST"),
                Port = Convert.ToInt32(Environment.GetEnvironmentVariable("RABBITMQ_PORT"))

            };

            // create connection  
            _connection = factory.CreateConnection();

            // create channel  
            _channel = _connection.CreateModel();
            _orderProcessedChannel = _connection.CreateModel();

            //_channel.ExchangeDeclare("demo.exchange", ExchangeType.Topic);
            _channel.QueueDeclare("orders", false, false, false, null);
            // _channel.QueueBind("demo.queue.log", "demo.exchange", "demo.queue.*", null);
            // _channel.BasicQos(0, 1, false);
            _orderProcessedChannel.QueueDeclare("order-processed", false, false, false, null);

            _connection.ConnectionShutdown += RabbitMQ_ConnectionShutdown;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var consumer = new EventingBasicConsumer(_channel);

            // On 'orders' queue receive
            consumer.Received += (ch, ea) =>
            {
                // received message  
                var content = System.Text.Encoding.UTF8.GetString(ea.Body.ToArray());

                _channel.BasicAck(ea.DeliveryTag, false);

                var order_content = JsonConvert.DeserializeObject<Cart>(content);

                // Update CartId AND OrderId
                order_content.cartId = Guid.NewGuid().ToString();
                order_content.orderId = Guid.NewGuid().ToString();
                order_content.orderStatus = "SUCCESS";

                // handle the received message  
                HandleMessage(order_content);

                var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(order_content));

                _orderProcessedChannel.BasicPublish(exchange: "",
                                     routingKey: "order-processed",
                                     basicProperties: null,
                                     body: body);
            };

            var orderProcessedConsumer = new EventingBasicConsumer(_orderProcessedChannel);

            //On 'order-processed' queue receive
            orderProcessedConsumer.Received += (ch, ea) =>
            {
                // received message  
                var content = System.Text.Encoding.UTF8.GetString(ea.Body.ToArray());


                _orderProcessedChannel.BasicAck(ea.DeliveryTag, false);

                var order_processed_content = JsonConvert.DeserializeObject<Cart>(content);

                //Update In-Memeory DB
                
                var entity = _context.Carts.FirstOrDefault(cart => cart.cartId == order_processed_content.cartId);

                if (entity != null)
                {
                    entity.orderStatus = order_processed_content.orderStatus;
                    _context.SaveChanges();
                }
                
            };

            consumer.Shutdown += OnConsumerShutdown;
            consumer.Registered += OnConsumerRegistered;
            consumer.Unregistered += OnConsumerUnregistered;
            consumer.ConsumerCancelled += OnConsumerConsumerCancelled;

            orderProcessedConsumer.Shutdown += OnConsumerShutdown;
            orderProcessedConsumer.Registered += OnConsumerRegistered;
            orderProcessedConsumer.Unregistered += OnConsumerUnregistered;
            orderProcessedConsumer.ConsumerCancelled += OnConsumerConsumerCancelled;

            _channel.BasicConsume("orders", false, consumer);
            _orderProcessedChannel.BasicConsume("order-processed", false, orderProcessedConsumer);
            return Task.CompletedTask;
        }

        private async void HandleMessage(Cart record)
        {
            // Store into In-Memeory DB
            _logger.LogInformation($"consumer received");

            // Stored in In-Memeroy DB
            if (record.orderId != null)
            {
                _context.Add(record);
                await _context.SaveChangesAsync();
            }


        }

        private void OnConsumerConsumerCancelled(object sender, ConsumerEventArgs e) { }
        private void OnConsumerUnregistered(object sender, ConsumerEventArgs e) { }
        private void OnConsumerRegistered(object sender, ConsumerEventArgs e) { }
        private void OnConsumerShutdown(object sender, ShutdownEventArgs e) { }
        private void RabbitMQ_ConnectionShutdown(object sender, ShutdownEventArgs e) { }

        public override void Dispose()
        {
            _channel.Close();
            _orderProcessedChannel.Close();
            _connection.Close();
            base.Dispose();
        }
    }
}
