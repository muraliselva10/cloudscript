# Cloudscript for Dokku latest version 
cloudscript dokku
    version                     = '2014_12_15'
    result_template             = dokku_result_template

globals
    dokku_hostname                = 'dokku'
    dokku_instance_type           = 'CS1' # 1Gb, 1 core, 25Gb, 1Gbps
    dokku_image_type              = 'Ubuntu Server 14.04 LTS'
    dokku_slice_user              = 'dokku'
    # Password setup
    server_password             = lib::random_password()
    console_password            = lib::random_password()

thread elk_setup
    tasks                       = [dokku_server_setup]

task dokku_server_setup

    #
    # Create dokku keys
    #

    # Create dokku server root password key
    /key/password dokku_server_password_key read_or_create
        key_group               = _SERVER
        password                = server_password

    # Create dokku server console key

    /key/password dokku_server_console_key read_or_create
        key_group               = _CONSOLE
        password                = console_password

    #
    # Create dokku storage slice, bootstrap script and recipe
    #

    # Create storage slice keys
    /key/token dokku_slice_key read_or_create
        username                = dokku_slice_user

    # Create slice to store script in cloudstorage

    /storage/slice dokku_slice read_or_create
        keys                    = [dokku_slice_key]

    # Create slice container to store script in cloudstorage

    /storage/container dokku_container => [dokku_slice] read_or_create
        slice                   = dokku_slice

    # Place script data in cloudstorage

    /storage/object dokku_bootstrap_object => [dokku_slice, dokku_container] read_or_create
        container_name          = 'dokku_container'
        file_name               = 'bootstrap_dokku.sh'
        slice                   = dokku_slice
        content_data            = dokku_bootstrap_data

    # Associate the cloudstorage object with the dokku script

    /orchestration/script dokku_bootstrap_script => [dokku_slice, dokku_container, dokku_bootstrap_object] read_or_create
        data_uri                = 'cloudstorage://dokku_slice/dokku_container/bootstrap_dokku.sh'
        script_type             = _SHELL
        encoding                = _STORAGE

    # Create the recipe and associate the script

    /orchestration/recipe dokku_bootstrap_recipe read_or_create
        scripts                 = [dokku_bootstrap_script]

    #
    # Create the dokku server
    #

    /server/cloud dokku_server read_or_create
        hostname                = '{{ dokku_hostname }}'
        image                   = '{{ dokku_image_type }}'
        service_type            = '{{ dokku_instance_type }}'
        keys                    = [dokku_server_password_key, dokku_server_console_key]
        recipes                 = [dokku_bootstrap_recipe]

text_template dokku_bootstrap_data
#!/bin/sh

# check if running as root
[ `whoami` = 'root' ] || {
    echo "ERROR: must have root permissions to execute the commands"
    exit 1
}

#Do apt work with HTTPS?
[ -e /usr/lib/apt/methods/https ] || {
    apt-get update
    apt-get install apt-transport-https -y
}

#Download Dokku bootstrap script and install latest version
wget -qO- https://raw.github.com/progrium/dokku/v0.3.9/bootstrap.sh | sudo DOKKU_BRANCH=master bash

#Prepare docker config
echo 'DOCKER_OPTS="--bip=172.17.42.1/26"' >> /etc/default/docker
service docker stop
ip link delete docker0
service docker start

_eof

text_template dokku_result_template

Your Dokku server is ready at the following IP address:

{{ dokku_server.ipaddress_public }}
login:    root
password: {{ server_password }}

_eof
