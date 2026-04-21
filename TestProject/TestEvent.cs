using Shared.Infrastructure.Events;
using TestProject.Models.EventModels;

namespace TestProject
{
    public class Tests
    {
        IEventAggregator _eventAggregator;
        [SetUp]
        public void Setup()
        {
            _eventAggregator = new EventAggregator();
            _eventAggregator.GetEvent<MyMessage>().Subscribe(HandleMessage);
        }

        private void HandleMessage(string obj)
        {
            Console.WriteLine(obj);
        }

        [Test]
        public void TestMessageEvent()
        {
            _eventAggregator.GetEvent<MyMessage>().Publish("dfdfdffdf");
            Assert.Pass();
        }
    }
}