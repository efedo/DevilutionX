#include <atomic>
#include <cstdint>
#include <string>
#include <thread>

#include <asio/ip/tcp.hpp>

#include <gtest/gtest.h>

#include "devilution.pb.h"
#include "network/authoritative/authoritative_client.hpp"
#include "network/authoritative/envelope_codec.hpp"

namespace devilution::authoritative {
namespace {

using asio::ip::tcp;
namespace protocol = ::devilution::protocol::v1;

void WriteEnvelope(tcp::socket &socket, const protocol::Envelope &envelope)
{
	const std::string payload = envelope.SerializeAsString();
	ASSERT_TRUE(EnvelopeCodec::Write(socket, std::span<const uint8_t>(
	                                      reinterpret_cast<const uint8_t *>(payload.data()), payload.size()))
	                .has_value());
}

TEST(AuthoritativeClient, CompletesHandshakeCommandAndSnapshotExchange)
{
	asio::io_context serverIo;
	tcp::acceptor acceptor { serverIo, { tcp::v4(), 0 } };
	std::atomic_bool serverObservedExpectedMessages = false;
	std::thread server([&]() {
		tcp::socket socket(serverIo);
		acceptor.accept(socket);

		auto helloPayload = EnvelopeCodec::Read(socket);
		if (!helloPayload.has_value() || !helloPayload->has_value())
			return;
		protocol::Envelope hello;
		if (!hello.ParseFromArray(helloPayload->value().data(), static_cast<int>(helloPayload->value().size()))
		    || hello.payload_case() != protocol::Envelope::kClientHello)
			return;
		if (hello.client_hello().protocol_schema_version() != "1"
		    || hello.client_hello().content_manifest_hash() != "content")
			return;

		protocol::Envelope serverHello;
		serverHello.mutable_server_hello()->set_server_build_id("server");
		serverHello.mutable_server_hello()->set_protocol_schema_version("1");
		serverHello.mutable_server_hello()->set_content_manifest_hash("content");
		serverHello.mutable_server_hello()->set_tick_rate_hz(20);
		serverHello.mutable_server_hello()->set_session_token("session");
		WriteEnvelope(socket, serverHello);

		protocol::Envelope initialSnapshot;
		initialSnapshot.mutable_snapshot()->set_tick(11);
		initialSnapshot.mutable_snapshot()->set_state_sha256("initial-state");
		WriteEnvelope(socket, initialSnapshot);

		auto commandPayload = EnvelopeCodec::Read(socket);
		if (!commandPayload.has_value() || !commandPayload->has_value())
			return;
		protocol::Envelope command;
		if (!command.ParseFromArray(commandPayload->value().data(), static_cast<int>(commandPayload->value().size()))
		    || command.payload_case() != protocol::Envelope::kCommandBatch
		    || command.command_batch().commands_size() != 1
		    || command.command_batch().commands(0).client_sequence() != 7)
			return;

		protocol::Envelope acknowledgement;
		auto *result = acknowledgement.mutable_command_ack()->add_results();
		result->set_client_sequence(7);
		result->set_status(protocol::COMMAND_STATUS_ACCEPTED);
		result->set_applied_tick(12);
		WriteEnvelope(socket, acknowledgement);

		protocol::Envelope snapshot;
		snapshot.mutable_snapshot()->set_tick(12);
		snapshot.mutable_snapshot()->set_state_sha256("state");
		WriteEnvelope(socket, snapshot);
		serverObservedExpectedMessages = true;
	});

	AuthoritativeClient::Configuration configuration {
		.host = "127.0.0.1",
		.port = acceptor.local_endpoint().port(),
		.clientBuildId = "client",
		.protocolSchemaVersion = "1",
		.contentManifestHash = "content",
		.expectInitialSnapshot = true,
	};
	auto client = AuthoritativeClient::Connect(configuration);
	ASSERT_TRUE(client.has_value()) << client.error();
	ASSERT_EQ((*client)->ServerHello().session_token(), "session");
	auto initialSnapshot = (*client)->ReadSnapshot();
	ASSERT_TRUE(initialSnapshot.has_value()) << initialSnapshot.error();
	EXPECT_EQ(initialSnapshot->tick(), 11U);
	EXPECT_EQ(initialSnapshot->state_sha256(), "initial-state");

	protocol::CommandBatch batch;
	auto *command = batch.add_commands();
	command->set_client_sequence(7);
	command->set_requested_tick(12);
	command->mutable_move_requested()->set_direction_x(1);
	auto acknowledgement = (*client)->Submit(batch);
	ASSERT_TRUE(acknowledgement.has_value()) << acknowledgement.error();
	ASSERT_EQ(acknowledgement->results_size(), 1);
	EXPECT_EQ(acknowledgement->results(0).status(), protocol::COMMAND_STATUS_ACCEPTED);

	auto snapshot = (*client)->ReadSnapshot();
	ASSERT_TRUE(snapshot.has_value()) << snapshot.error();
	EXPECT_EQ(snapshot->tick(), 12U);
	EXPECT_EQ(snapshot->state_sha256(), "state");

	(*client)->Close();
	server.join();
	EXPECT_TRUE(serverObservedExpectedMessages);
}

} // namespace
} // namespace devilution::authoritative
