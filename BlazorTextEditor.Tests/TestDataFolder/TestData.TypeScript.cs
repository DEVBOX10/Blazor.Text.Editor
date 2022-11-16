﻿namespace BlazorTextEditor.Tests.TestDataFolder;

public static partial class TestData
{
    public const string TYPE_SCRIPT_EXAMPLE_TEXT = @"import { SocketMessage, User } from ""../contracts/events"";

import socketIOClient from ""socket.io-client"";

const socketClient = socketIOClient();

interface EmitterCallback<T> {
  (data: T): void;
}

interface WrappedClientSocket<T> {
  emit: (data: T) => SocketIOClient.Socket;
  on: (callback: EmitterCallback<T>) => SocketIOClient.Emitter;
  off: (callback: EmitterCallback<T>) => SocketIOClient.Emitter;
}

function createSocket<T>(event: SocketMessage): WrappedClientSocket<T> {
  return {
    emit: (data) => socketClient.emit(event, data),
    on: (callback) => socketClient.on(event, callback),
    off: (callback) => socketClient.off(event, callback),
  };
}

const chatMessageEvent: WrappedClientSocket<string> =
  createSocket(""chat_message"");
const userConnectedSocket: WrappedClientSocket<User> =
  createSocket(""user_connected"");";
}