#pragma once

/**
 * @file network/authoritative/server_backed_client.hpp
 *
 * Opt-in C++ client for the C# authoritative server.
 */

#include <cstdint>
#include <map>
#include <memory>
#include <optional>
#include <string>
#include <vector>

#include <asio/ip/tcp.hpp>

#include <expected.hpp>

#include "devilution.pb.h"
#include "network/protocol/command_delivery.hpp"

namespace devilution::authoritative {

namespace protocol = ::devilution::protocol::v1;

class ServerBackedClient {
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

	static tl::expected<std::unique_ptr<ServerBackedClient>, std::string> Connect(Configuration configuration);

	~ServerBackedClient();

	ServerBackedClient(const ServerBackedClient &) = delete;
	ServerBackedClient &operator=(const ServerBackedClient &) = delete;

	const protocol::ServerHello &ServerHello() const noexcept { return serverHello_; }

	/** Resumes the current server session and resubmits unresolved commands. */
	tl::expected<void, std::string> Reconnect(uint64_t nowMs);

	tl::expected<protocol::CommandAck, std::string> Submit(const protocol::CommandBatch &batch);
	tl::expected<protocol::Snapshot, std::string> ReadSnapshot();

	/** Adds a command and assigns its session-scoped retry sequence. */
	uint64_t QueueCommand(protocol::Command command);

	/** Sends every queued command that has not been sent yet. */
	tl::expected<void, std::string> SendQueuedCommands(uint64_t nowMs);

	/** Resends commands whose adaptive acknowledgement timeout has elapsed. */
	tl::expected<std::vector<uint64_t>, std::string> PrepareTrackedResubmissions(uint64_t nowMs);

	/** Reads and applies one server acknowledgement batch. */
	tl::expected<protocol::CommandAck, std::string> ReceiveCommandAcknowledgement(uint64_t nowMs);

	[[nodiscard]] std::size_t PendingTrackedCommandCount() const noexcept { return pendingCommands_.size(); }

	void Close() noexcept;

private:
	ServerBackedClient();

	tl::expected<void, std::string> ConnectTransport(bool expectInitialSnapshot);
	tl::expected<protocol::Envelope, std::string> ReadEnvelope();
	tl::expected<void, std::string> WriteEnvelope(const protocol::Envelope &envelope);

	asio::io_context ioContext_;
	asio::ip::tcp::resolver resolver_ { ioContext_ };
	asio::ip::tcp::socket socket_ { ioContext_ };
	Configuration configuration_;
	protocol::ServerHello serverHello_;
	std::optional<protocol::Snapshot> pendingSnapshot_;
	CommandDeliveryTracker deliveryTracker_;
	struct PendingCommand {
		protocol::Command command;
		bool sent = false;
	};
	std::map<uint64_t, PendingCommand> pendingCommands_;
};

} // namespace devilution::authoritative
