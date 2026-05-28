using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using Shared.Abstractions.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shared.Models.Communication;
using Shared.Models.Log;
using Shared.Abstractions.ICommunication;

namespace Shared.Infrastructure.Communication
{
    [CommunicationAdapter(typeof(RabbitMqRpcServerRuntimeConfig))]
    public class RabbitMQRPCServer : CommunicationBase, ICommunication
    {
        #region Propertys
        private IConnection _Connection;
        private IModel _Channel;
        private EventingBasicConsumer _Consumer;
        private ConnectionFactory _Factory;


        #endregion
        #region 构造
        public RabbitMQRPCServer(RabbitMqRpcServerRuntimeConfig config)
        {
            LocalName = config.LocalName;
            _Factory = new ConnectionFactory() { HostName = config.RemoteIpAddress, Port = config.RemotePort, UserName = config.UserName, Password = config.Password };
        }
        #endregion
        #region 方法
        public override bool Close()
        {
            if (_Connection != null && _Connection.IsOpen)
            {
                _Connection?.Close();
                _Connection?.Dispose();
                IsConnected = ConnectState.DisConnected;
            }
            WriteLog(new LogMessageModel() { Message = $"RabbitMQ 通讯断开{(!_Connection.IsOpen ? "成功" : "失败")}！", Type = Abstractions.Enum.LogType.WARN });
            return true;
        }



        public override bool Start()
        {
            _Connection = _Factory.CreateConnection();
            _Channel = _Connection.CreateModel();
            _Channel.QueueDeclare(queue: "rpc_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);
            _Channel.BasicQos(0, 1, false);
            _Consumer = new EventingBasicConsumer(_Channel);
            var consumer = new EventingBasicConsumer(_Channel);
            consumer.Received += (model, ea) =>
            {
                Task.Run(() =>
                {
                    string response = null;
                    var body = ea.Body;
                    var props = ea.BasicProperties;
                    var replyProps = _Channel.CreateBasicProperties();
                    replyProps.CorrelationId = props.CorrelationId;
                    try
                    {
                        var message = Encoding.UTF8.GetString(body.ToArray());
                        WriteLog(new LogMessageModel() { Message = $"ClientId:{props.CorrelationId}-->Server:{message}", Type = Abstractions.Enum.LogType.INFO });
                        response = RaiseReceive(message, props.CorrelationId);
                    }
                    catch (Exception e)
                    {
                        WriteLog(new LogMessageModel() { Message = $"处理RabbitMQ消息失败：{e.Message}", Type = Abstractions.Enum.LogType.ERROR });
                    }
                    finally
                    {
                        if (response != null)
                        {
                            WriteLog(new LogMessageModel() { Message = $"Server-->ClientId:{replyProps.CorrelationId}:{response}", Type = Abstractions.Enum.LogType.INFO });
                            var responseBytes = Encoding.UTF8.GetBytes(response);
                            try
                            {
                                if (string.IsNullOrEmpty(props.ReplyTo))
                                {
                                    WriteLog(new LogMessageModel() { Message = $"RabbitMQ 通讯回复目标为空！", Type = Abstractions.Enum.LogType.ERROR });
                                }
                                else
                                {
                                    _Channel.BasicPublish(exchange: "", routingKey: props.ReplyTo, basicProperties: replyProps, body: responseBytes);
                                }
                                _Channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                            }
                            catch (Exception)
                            {
                                IsConnected = ConnectState.DisConnected;
                            }

                        }
                    }
                });
            };
            _Channel.BasicConsume(queue: "rpc_queue", autoAck: false, consumer: consumer);
            WriteLog(new LogMessageModel() { Message = $"RabbitMQ 通讯{(_Connection.IsOpen ? "成功" : "失败")}！", Type = Abstractions.Enum.LogType.INFO });
            IsConnected = ConnectState.Connected;
            return true;
        }

        public bool Receive(ref SendReceiveModel readWriteModel)
        {
            throw new NotImplementedException();
        }
        public bool Send(ref SendReceiveModel readWriteModel, bool isWait = false)
        {
            throw new NotImplementedException();
        }

        public Task<bool> SendAsync(SendReceiveModel readWriteModel)
        {
            throw new NotImplementedException();
        }
        #endregion
        #region 事件
        #endregion
    }
}
