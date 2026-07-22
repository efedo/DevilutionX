#pragma once

/**
 * @file network/authoritative/authoritative_client.hpp
 *
 * Opt-in C++ client for the C# authoritative Protobuf server.
 */

#include <cstdint>
#include <memory>
#include <optional>
#include <string>

#include <asio/ip/tcp.hpp>

#include <expected.hpp>

#include "devilution.pb.h"

namespace devilution::authoritative {

namespace protocol = ::devilution::protocol::v1;

class AuthoritativeClient {
public:
	struct Configuration {
		std::string host;
		uint16_t port = 0;
		std::string clientBuildId;
		std::string protocolSchemaVersion;
		std::string contentManifestHash;
		std::string resumeToken;
		bool expectInitialSnapshot = false;
	};

	static tl::expected<std::unique_ptr<AuthoritativeClient>, std::string> Connect(Configuration configuration);

	~AuthoritativeClient();

	AuthoritativeClient(const AuthoritativeClient &) = delete;
	AuthoritativeClient &operator=(const AuthoritativeClient &) = delete;

	const protocol::ServerHello &ServerHello() const noexcept { return serverHello_; }

	tl::expected<protocol::CommandAck, std::string> Submit(const protocol::CommandBatch &batch);
	tl::expected<protocol::Snapshot, std::string> ReadSnapshot();

	void Close() noexcept;

private:
	AuthoritativeClient();

	tl::expected<protocol::Envelope, std::string> ReadEnvelope();
	tl::expected<void, std::string> WriteEnvelope(const protocol::Envelope &envelope);

	asio::io_context ioContext_;
	asio::ip::tcp::resolver resolver_ { ioContext_ };
	asio::ip::tcp::socket socket_ { ioContext_ };
	protocol::ServerHello serverHello_;
	std::optional<protocol::Snapshot> pendingSnapshot_;
};

} // namespace devilution::authoritative
