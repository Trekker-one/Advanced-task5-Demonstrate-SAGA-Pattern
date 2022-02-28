using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

            //_channel.ExchangeDeclare("demo.exchange", ExchangeType.Topic);
            _channel.QueueDeclare("orders", false, false, false, null);
            // _channel.QueueBind("demo.queue.log", "demo.exchange", "demo.queue.*", null);
            // _channel.BasicQos(0, 1, false);

            _connection.ConnectionShutdown += RabbitMQ_ConnectionShutdown;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var consumer = new EventingBasicConsumer(_channel);

            Cart record = new Cart();

            consumer.Received += (ch, ea) =>
            {
                // received message  
                var content = System.Text.Encoding.UTF8.GetString(ea.Body.ToArray());

                // handle the received message  
                HandleMessage(content);
                _channel.BasicAck(ea.DeliveryTag, false);
            };

            consumer.Shutdown += OnConsumerShutdown;
            consumer.Registered += OnConsumerRegistered;
            consumer.Unregistered += OnConsumerUnregistered;
            consumer.ConsumerCancelled += OnConsumerConsumerCancelled;

            _channel.BasicConsume("orders", true, consumer);
            return Task.CompletedTask;
        }

        private async void HandleMessage(string content)
        {
            // Store into In-Memeory DB
            var record = JsonConvert.DeserializeObject<Cart>(content);  
            _logger.LogInformation($"consumer received {content}");

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
            _connection.Close();
            base.Dispose();
        }
    }
}
