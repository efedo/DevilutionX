/**
 * @file network/authoritative/envelope_codec.cpp
 *
 * Implementation of authoritative transport framing.
 */

#include "network/authoritative/envelope_codec.hpp"

#include <array>

#include <asio/read.hpp>
#include <asio/write.hpp>

namespace devilution::authoritative {
namespace {

constexpr size_t HeaderSize = sizeof(uint32_t);

std::string SocketError(std::string_view operation, const asio::error_code &error)
{
	return std::string(operation) + ": " + error.message();
}

bool IsValidLength(uint32_t length)
{
	return length > 0 && length <= EnvelopeCodec::MaxEnvelopeBytes;
}

} // namespace

EnvelopeCodec::WriteResult EnvelopeCodec::Write(asio::ip::tcp::socket &socket, std::span<const uint8_t> payload)
{
	if (payload.size() == 0 || payload.size() > MaxEnvelopeBytes)
		return tl::make_unexpected(std::string("Envelope length is outside the allowed range."));
	const auto length = static_cast<uint32_t>(payload.size());

	std::array<uint8_t, HeaderSize> header {
		static_cast<uint8_t>(length),
		static_cast<uint8_t>(length >> 8),
		static_cast<uint8_t>(length >> 16),
		static_cast<uint8_t>(length >> 24),
	};
	asio::error_code error;
	asio::write(socket, asio::buffer(header), error);
	if (error)
		return tl::make_unexpected(SocketError("Writing envelope header", error));
	asio::write(socket, asio::buffer(payload.data(), payload.size()), error);
	if (error)
		return tl::make_unexpected(SocketError("Writing envelope payload", error));
	return {};
}

EnvelopeCodec::ReadResult EnvelopeCodec::Read(asio::ip::tcp::socket &socket)
{
	std::array<uint8_t, HeaderSize> header {};
	asio::error_code error;
	const size_t bytesRead = asio::read(socket, asio::buffer(header), error);
	if (error == asio::error::eof && bytesRead == 0)
		return std::optional<Payload> {};
	if (error)
		return tl::make_unexpected(bytesRead == 0
		        ? SocketError("Reading envelope header", error)
		        : "The envelope header was truncated.");

	const uint32_t length = static_cast<uint32_t>(header[0])
	    | (static_cast<uint32_t>(header[1]) << 8)
	    | (static_cast<uint32_t>(header[2]) << 16)
	    | (static_cast<uint32_t>(header[3]) << 24);
	if (!IsValidLength(length))
		return tl::make_unexpected(std::string("Envelope length is outside the allowed range."));

	Payload payload(length);
	asio::read(socket, asio::buffer(payload), error);
	if (error)
		return tl::make_unexpected(error == asio::error::eof
		        ? "The envelope payload was truncated."
		        : SocketError("Reading envelope payload", error));
	return std::optional<Payload> { std::move(payload) };
}

} // namespace devilution::authoritative
