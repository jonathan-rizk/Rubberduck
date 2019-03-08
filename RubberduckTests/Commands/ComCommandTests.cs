﻿using System;
using Moq;
using Moq.Protected;
using NLog;
using NUnit.Framework;
using Rubberduck.UI.Command.ComCommands;
using Rubberduck.VBEditor.Events;
using Rubberduck.VBEditor.SafeComWrappers.Abstract;

namespace RubberduckTests.Commands
{
    [TestFixture]
    [Category("ComCommand")]
    public class ComCommandTests
    {
        [Test]
        public void Verify_CanExecute_Before_And_After_Termination()
        {
            var (command, vbeEvents) = ArranageComCommand();
            vbeEvents.SetupSequence(v => v.Terminated).Returns(false).Returns(true);

            Assert.IsTrue(command.CanExecute(null));
            Assert.IsFalse(command.CanExecute(null));
        }

        [Test]
        public void Verify_OnExecute_Before_And_After_Termination()
        {
            var (command, vbeEvents) = ArranageComCommand();

            vbeEvents.SetupSequence(v => v.Terminated).Returns(false).Returns(true);
            command.Execute(null);
            command.Execute(null);

            command.VerifyOnExecute(Times.Once());
        }

        [Test]
        public void Verify_NoExecution_Terminated_BeforeCreation()
        {
            var vbe = new Mock<IVBE>();
            var vbeEvents = VbeEvents.Initialize(vbe.Object);
            
            VbeEvents.Terminate();
            var command = ArranageComCommand(vbeEvents);
            command.Execute(null);

            command.VerifyOnExecute(Times.Never());
        }

        [Test]
        public void Verify_Execution_Among_Instances()
        {
            var vbe = new Mock<IVBE>();
            var vbeEvents = VbeEvents.Initialize(vbe.Object);

            var command1 = ArranageComCommand(vbeEvents);
            command1.Execute(null);
            VbeEvents.Terminate();
            var command2 = ArranageComCommand(vbeEvents);
            command2.Execute(null);

            command1.VerifyOnExecute(Times.Once());
            command2.VerifyOnExecute(Times.Never());
        }

        private static (ComCommandBase comCommand, Mock<IVbeEvents> vbeEvents) ArranageComCommand()
        {
            var vbeEvents = new Mock<IVbeEvents>();
            return (ArranageComCommand(vbeEvents.Object), vbeEvents);
        }

        private static ComCommandBase ArranageComCommand(IVbeEvents vbeEvents)
        {
            var logger = new Mock<ILogger>();

            // The ComCommandBase is an abstract class and is the subject under the test
            // Therefore, we actually want to use Moq.Mock to create an implementation
            // to directly test the base class' behaviors. We should not modify the mock
            // behavior, hence why we return the object, rather than the mock. 
            var mockComCommand = new Mock<ComCommandBase>(logger.Object, vbeEvents)
            {
                CallBase = true
            };
            return mockComCommand.Object;
        }
    }

    internal static class ComCommandExtensions
    {
        internal static void VerifyOnExecute(this ComCommandBase comCommand, Times times)
        {
            var mock = Mock.Get(comCommand);
            mock.Protected().Verify("OnExecute", times, ItExpr.IsAny<object>());
        }
    }
}

