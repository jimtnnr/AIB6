--
-- PostgreSQL database dump
--

\restrict R46cEIFlVuyPwmD63UMmXSSHa9WqQ2QAkbqwbZn9eL42sTVw4P5Bj1zhVuhTPuW

-- Dumped from database version 16.13 (Ubuntu 16.13-0ubuntu0.24.04.1)
-- Dumped by pg_dump version 16.13 (Ubuntu 16.13-0ubuntu0.24.04.1)

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: public; Type: SCHEMA; Schema: -; Owner: airlock
--

-- *not* creating schema, since initdb creates it


ALTER SCHEMA public OWNER TO airlock;

--
-- Name: get_draft_archive_page(integer, integer, text, text, text, boolean); Type: FUNCTION; Schema: public; Owner: airlock
--

CREATE FUNCTION public.get_draft_archive_page(page integer, size integer, sort_column text, sort_direction text, filter text, show_hidden boolean) RETURNS TABLE(id integer, filename text, letter_type text, "timestamp" timestamp without time zone, favorite boolean, hidden boolean)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    SELECT
        letters.id,
        letters.filename,
        letters.letter_type,
        letters."timestamp",
        letters.favorite,
        letters.hidden
    FROM letters
    WHERE
        (show_hidden = TRUE OR letters.hidden = FALSE)
        AND (
            filter IS NULL
            OR filter = ''
            OR letters.filename ILIKE '%' || filter || '%'
            OR letters.letter_type ILIKE '%' || filter || '%'
        )
    ORDER BY letters."timestamp" DESC
    LIMIT size
    OFFSET ((page - 1) * size);
END;
$$;


ALTER FUNCTION public.get_draft_archive_page(page integer, size integer, sort_column text, sort_direction text, filter text, show_hidden boolean) OWNER TO airlock;

--
-- Name: get_letters(); Type: FUNCTION; Schema: public; Owner: airlock
--

CREATE FUNCTION public.get_letters() RETURNS TABLE(id integer, filename text, letter_type text, "timestamp" timestamp without time zone, favorite boolean, hidden boolean)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    SELECT
        letters.id,
        letters.filename,
        letters.letter_type,
        letters."timestamp",
        letters.favorite,
        letters.hidden
    FROM letters
    ORDER BY letters."timestamp" DESC;
END;
$$;


ALTER FUNCTION public.get_letters() OWNER TO airlock;

--
-- Name: insert_letter(text, text, timestamp without time zone, boolean, boolean); Type: PROCEDURE; Schema: public; Owner: airlock
--

CREATE PROCEDURE public.insert_letter(IN p_filename text, IN p_letter_type text, IN p_timestamp timestamp without time zone, IN p_favorite boolean, IN p_hidden boolean)
    LANGUAGE plpgsql
    AS $$
BEGIN
    INSERT INTO letters (
        filename,
        letter_type,
        timestamp,
        favorite,
        hidden
    )
    VALUES (
        p_filename,
        p_letter_type,
        p_timestamp,
        p_favorite,
        p_hidden
    );
END;
$$;


ALTER PROCEDURE public.insert_letter(IN p_filename text, IN p_letter_type text, IN p_timestamp timestamp without time zone, IN p_favorite boolean, IN p_hidden boolean) OWNER TO airlock;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: draft_archive; Type: TABLE; Schema: public; Owner: airlock
--

CREATE TABLE public.draft_archive (
    id integer NOT NULL,
    filename text NOT NULL,
    letter_type text NOT NULL,
    "timestamp" timestamp without time zone NOT NULL,
    favorite boolean DEFAULT false NOT NULL,
    hidden boolean DEFAULT false NOT NULL
);


ALTER TABLE public.draft_archive OWNER TO airlock;

--
-- Name: draft_archive_id_seq; Type: SEQUENCE; Schema: public; Owner: airlock
--

CREATE SEQUENCE public.draft_archive_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.draft_archive_id_seq OWNER TO airlock;

--
-- Name: draft_archive_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: airlock
--

ALTER SEQUENCE public.draft_archive_id_seq OWNED BY public.draft_archive.id;


--
-- Name: letters; Type: TABLE; Schema: public; Owner: airlock
--

CREATE TABLE public.letters (
    id integer NOT NULL,
    filename text NOT NULL,
    letter_type text NOT NULL,
    "timestamp" timestamp without time zone NOT NULL,
    favorite boolean DEFAULT false NOT NULL,
    hidden boolean DEFAULT false NOT NULL
);


ALTER TABLE public.letters OWNER TO airlock;

--
-- Name: letters_id_seq; Type: SEQUENCE; Schema: public; Owner: airlock
--

CREATE SEQUENCE public.letters_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.letters_id_seq OWNER TO airlock;

--
-- Name: letters_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: airlock
--

ALTER SEQUENCE public.letters_id_seq OWNED BY public.letters.id;


--
-- Name: draft_archive id; Type: DEFAULT; Schema: public; Owner: airlock
--

ALTER TABLE ONLY public.draft_archive ALTER COLUMN id SET DEFAULT nextval('public.draft_archive_id_seq'::regclass);


--
-- Name: letters id; Type: DEFAULT; Schema: public; Owner: airlock
--

ALTER TABLE ONLY public.letters ALTER COLUMN id SET DEFAULT nextval('public.letters_id_seq'::regclass);


--
-- Data for Name: draft_archive; Type: TABLE DATA; Schema: public; Owner: airlock
--

COPY public.draft_archive (id, filename, letter_type, "timestamp", favorite, hidden) FROM stdin;
\.


--
-- Data for Name: letters; Type: TABLE DATA; Schema: public; Owner: airlock
--

COPY public.letters (id, filename, letter_type, "timestamp", favorite, hidden) FROM stdin;
1	Appeal_InsuranceClaimAppeal_Reminder_Medium_20260516_141533.txt	Legal Letters > Appeal	2026-05-16 14:15:33.718627	f	f
3	Appeal_InsuranceClaimAppeal_Reminder_Medium_20260516_213014.txt	Legal Letters > Appeal	2026-05-16 21:30:14.388874	f	f
5	Appeal_InsuranceClaimAppeal_Reminder_Medium_20260516_213214.txt	Legal Letters > Appeal	2026-05-16 21:32:14.244168	f	t
4	Appeal_InsuranceClaimAppeal_Reminder_Medium_20260516_213205.txt	Legal Letters > Appeal	2026-05-16 21:32:05.957745	t	f
6	Appeal_InsuranceClaimAppeal_Reminder_Medium_20260517_113027.txt	Legal Letters > Appeal	2026-05-17 11:30:27.158992	f	t
2	Appeal_InsuranceClaimAppeal_Reminder_Medium_20260516_212820.txt	Legal Letters > Appeal	2026-05-16 21:28:20.171362	t	f
7	Appeal_InsuranceClaimAppeal_Reminder_Medium_20260517_130706.txt	Legal Letters > Appeal	2026-05-17 13:07:06.914818	f	f
8	Appeal_InsuranceClaimAppeal_Reminder_Medium_20260517_131027.txt	Legal Letters > Appeal	2026-05-17 13:10:27.425804	f	f
9	Appeal_InsuranceClaimAppeal_Reminder_Medium_20260519_091030.txt	Legal Letters > Appeal	2026-05-19 09:10:30.285787	f	f
10	Appeal_InsuranceClaimAppeal_Reminder_Medium_20260519_091339.txt	Legal Letters > Appeal	2026-05-19 09:13:39.909186	f	t
\.


--
-- Name: draft_archive_id_seq; Type: SEQUENCE SET; Schema: public; Owner: airlock
--

SELECT pg_catalog.setval('public.draft_archive_id_seq', 1, false);


--
-- Name: letters_id_seq; Type: SEQUENCE SET; Schema: public; Owner: airlock
--

SELECT pg_catalog.setval('public.letters_id_seq', 10, true);


--
-- Name: draft_archive draft_archive_pkey; Type: CONSTRAINT; Schema: public; Owner: airlock
--

ALTER TABLE ONLY public.draft_archive
    ADD CONSTRAINT draft_archive_pkey PRIMARY KEY (id);


--
-- Name: letters letters_pkey; Type: CONSTRAINT; Schema: public; Owner: airlock
--

ALTER TABLE ONLY public.letters
    ADD CONSTRAINT letters_pkey PRIMARY KEY (id);


--
-- PostgreSQL database dump complete
--

\unrestrict R46cEIFlVuyPwmD63UMmXSSHa9WqQ2QAkbqwbZn9eL42sTVw4P5Bj1zhVuhTPuW

