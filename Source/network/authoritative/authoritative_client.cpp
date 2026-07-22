/**
 * @file network/authoritative/authoritative_client.cpp
 *
 * Implementation of the opt-in authoritative client.
 */

#include "network/authoritative/authoritative_client.hpp"

#include <span>
#include <utility>

#include "network/authoritative/envelope_codec.hpp"

namespace devilution::authoritative {
namespace {

tl::expected<protocol::Envelope, std::string> ParseEnvelope(const EnvelopeCodec::Payload &payload)
{
	protocol::Envelope envelope;
	if (!envelope.ParseFromArray(payload.data(), static_cast<int>(payload.size())))
		return tl::make_unexpected(std::string("The envelope payload is not valid Protobuf."));
	return envelope;
}

std::string ProtocolErrorMessage(const protocol::Envelope &envelope)
{
	if (envelope.payload_case() == protocol::Envelope::kError)
		return "Authoritative server rejected the message: " + envelope.error().detail();
	return "Unexpected authoritative server message.";
}

} // namespace

AuthoritativeClient::AuthoritativeClient() = default;

tl::expected<std::unique_ptr<AuthoritativeClient>, std::string> AuthoritativeClient::Connect(Configuration configuration)
{
	if (configuration.host.empty() || configuration.port == 0
	    || configuration.clientBuildId.empty() || configuration.protocolSchemaVersion.empty()
	    || configuration.contentManifestHash.empty())
		return tl::make_unexpected(std::string("Authoritative client configuration is incomplete."));

	auto client = std::unique_ptr<AuthoritativeClient>(new AuthoritativeClient());
	asio::error_code error;
	const auto endpoints = client->resolver_.resolve(configuration.host, std::to_string(configuration.port), error);
	if (error)
		return tl::make_unexpected("Resolving authoritative server: " + error.message());
	asio::connect(client->socket_, endpoints, error);
	if (error)
		return tl::make_unexpected("Connecting to authoritative server: " + error.message());

	protocol::Envelope hello;
	auto *clientHello = hello.mutable_client_hello();
	clientHello->set_client_build_id(configuration.clientBuildId);
	clientHello->set_protocol_schema_version(configuration.protocolSchemaVersion);
	clientHello->set_content_manifest_hash(configuration.contentManifestHash);
	clientHello->set_resume_token(configuration.resumeToken);
	if (auto result = client->WriteEnvelope(hello); !result.has_value())
		return tl::make_unexpected(result.error());

	auto response = client->ReadEnvelope();
	if (!response.has_value())
		return tl::make_unexpected(response.error());
	if (response->payload_case() != protocol::Envelope::kServerHello)
		return tl::make_unexpected(ProtocolErrorMessage(*response));
	if (response->server_hello().protocol_schema_version() != configuration.protocolSchemaVersion
	    || response->server_hello().content_manifest_hash() != configuration.contentManifestHash)
		return tl::make_unexpected(std::string("Authoritative server returned incompatible protocol or content identity."));

	client->serverHello_ = response->server_hello();
	if (configuration.expectInitialSnapshot) {
		auto initialSnapshot = client->ReadEnvelope();
		if (!initialSnapshot.has_value())
			return tl::make_unexpected(initialSnapshot.error());
		if (initialSnapshot->payload_case() != protocol::Envelope::kSnapshot)
			return tl::make_unexpected(ProtocolErrorMessage(*initialSnapshot));
		client->pendingSnapshot_ = initialSnapshot->snapshot();
	}
	return client;
}

tl::expected<protocol::CommandAck, std::string> AuthoritativeClient::Submit(const protocol::CommandBatch &batch)
{
	protocol::Envelope request;
	*request.mutable_command_batch() = batch;
	if (auto result = WriteEnvelope(request); !result.has_value())
		return tl::make_unexpected(result.error());

	auto response = ReadEnvelope();
	if (!response.has_value())
		return tl::make_unexpected(response.error());
	if (response->payload_case() != protocol::Envelope::kCommandAck)
		return tl::make_unexpected(ProtocolErrorMessage(*response));
	return response->command_ack();
}

tl::expected<protocol::Snapshot, std::string> AuthoritativeClient::ReadSnapshot()
{
	if (pendingSnapshot_.has_value()) {
		protocol::Snapshot snapshot = std::move(*pendingSnapshot_);
		pendingSnapshot_.reset();
		return snapshot;
	}

	auto response = ReadEnvelope();
	if (!response.has_value())
		return tl::make_unexpected(response.error());
	if (response->payload_case() != protocol::Envelope::kSnapshot)
		return tl::make_unexpected(ProtocolErrorMessage(*response));
	return response->snapshot();
}

void AuthoritativeClient::Close() noexcept
{
	if (!socket_.is_open())
		return;
	asio::error_code ignored;
	socket_.shutdown(asio::ip::tcp::socket::shutdown_both, ignored);
	socket_.close(ignored);
}

AuthoritativeClient::~AuthoritativeClient()
{
	Close();
}

tl::expected<protocol::Envelope, std::string> AuthoritativeClient::ReadEnvelope()
{
	auto payload = EnvelopeCodec::Read(socket_);
	if (!payload.has_value())
		return tl::make_unexpected(payload.error());
	if (!payload->has_value())
		return tl::make_unexpected(std::string("Authoritative server closed the connection."));
	return ParseEnvelope(payload->value());
}

tl::expected<void, std::string> AuthoritativeClient::WriteEnvelope(const protocol::Envelope &envelope)
{
	const std::string payload = envelope.SerializeAsString();
	return EnvelopeCodec::Write(socket_, std::span<const uint8_t>(
	    reinterpret_cast<const uint8_t *>(payload.data()), payload.size()));
}

} // namespace devilution::authoritative
